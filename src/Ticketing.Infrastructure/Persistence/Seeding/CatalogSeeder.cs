using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ticketing.Domain.Catalog;

namespace Ticketing.Infrastructure.Persistence.Seeding;

/// <summary>
/// 15 個活動／15 個場次／45 個票區／2,880 個座位。
///
/// **ID 是算出來的、固定的**，重跑得到相同結果，整合測試可以直接寫 `1101`（設計文件 15 第 2 節）：
/// <list type="bullet">
/// <item>Sections：<c>performanceId × 10 + 區index</c>（A=1、B=2、C=3）</item>
/// <item>Seats：<c>performanceId × 1000 + 區index × 100 + (排−1) × 每排席數 + 座號</c></item>
/// </list>
/// 已有資料時**只核對不修改**：ID、code、父子關係、票區與座位幾何一致就保留，
/// 有差異整段失敗——不覆蓋日期、暫停狀態、庫存或訂單。
/// </summary>
public sealed class CatalogSeeder(TicketingDbContext db)
{
    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);

    public async Task<(int Events, int Performances, int Sections, int Seats)> SeedAsync(
        CatalogSeedFile file, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (await db.Events.AnyAsync(ct))
        {
            await VerifyExistingAsync(file, ct);
            return (0, 0, 0, 0);
        }

        var anchor = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);   // seed 當天 UTC 00:00
        var sections = 0;
        var seats = 0;

        foreach (var source in file.Events)
        {
            var @event = new Event(source.Id, source.Code, ParseCategory(source.Category), source.Title,
                                   source.Performer, source.Genre, source.Description, source.PublicImage);
            db.Events.Add(@event);

            var startsAt = StartsAt(anchor, source);
            var performance = new Performance(
                source.PerformanceId, @event, source.Venue, source.City,
                startsAt,
                anchor.AddDays(source.SalesOpenOffsetDays),
                startsAt.AddMinutes(-source.SalesCloseMinutesBeforeStart),
                source.DurationMinutes);
            db.Performances.Add(performance);

            for (var index = 0; index < source.Sections.Count; index++)
            {
                var s = source.Sections[index];
                var sectionIndex = index + 1;                                   // A=1、B=2、C=3
                var sectionId = source.PerformanceId * 10 + sectionIndex;

                db.Sections.Add(new Section(sectionId, source.PerformanceId, s.Code, s.Name,
                                            s.Price, s.Rows, s.SeatsPerRow));
                sections++;

                for (var row = 1; row <= s.Rows; row++)
                {
                    for (var number = 1; number <= s.SeatsPerRow; number++)
                    {
                        var seatId = source.PerformanceId * 1000
                                   + sectionIndex * 100
                                   + (row - 1) * s.SeatsPerRow
                                   + number;
                        db.Seats.Add(new Seat(seatId, source.PerformanceId, sectionId, row, number));
                        seats++;
                    }
                }
            }
        }

        return (file.Events.Count, file.Events.Count, sections, seats);
    }

    /// <summary>時刻以台北時間解讀，再轉成 UTC 存。</summary>
    private static DateTimeOffset StartsAt(DateTimeOffset anchor, CatalogSeedEvent source)
    {
        if (!TimeOnly.TryParse(source.StartsAtTaipei, CultureInfo.InvariantCulture, out var timeOfDay))
            throw new InvalidOperationException($"{source.Code} 的 startsAtTaipei 格式不正確：{source.StartsAtTaipei}");

        // 這裡要表達的是「台北的牆上時鐘時間」，所以 Kind 必須是 Unspecified。
        // 直接用 anchor.UtcDateTime.Date 會帶著 Kind=Utc，配上 +08:00 的 offset 會擲例外。
        var wallClock = DateTime.SpecifyKind(
            anchor.UtcDateTime.Date.AddDays(source.StartsAfterDays).Add(timeOfDay.ToTimeSpan()),
            DateTimeKind.Unspecified);

        return new DateTimeOffset(wallClock, TaipeiOffset).ToUniversalTime();
    }

    private static EventCategory ParseCategory(string value)
        => Enum.TryParse<EventCategory>(value, ignoreCase: false, out var category)
            ? category
            : throw new InvalidOperationException($"未知的活動類別：{value}");

    /// <summary>已有 catalog 時的一致性核對。差異就整段失敗，不試圖修補。</summary>
    private async Task VerifyExistingAsync(CatalogSeedFile file, CancellationToken ct)
    {
        var events = await db.Events.AsNoTracking().ToDictionaryAsync(e => e.Id, ct);
        var sections = await db.Sections.AsNoTracking().ToDictionaryAsync(s => s.Id, ct);
        var seatCounts = await db.Seats.AsNoTracking()
                                 .GroupBy(s => s.SectionId)
                                 .Select(g => new { SectionId = g.Key, Count = g.Count() })
                                 .ToDictionaryAsync(x => x.SectionId, x => x.Count, ct);

        var problems = new List<string>();

        if (events.Count != file.Events.Count)
            problems.Add($"活動數不符：資料庫 {events.Count}、seed 檔 {file.Events.Count}");

        foreach (var source in file.Events)
        {
            if (!events.TryGetValue(source.Id, out var existing))
            {
                problems.Add($"活動 {source.Id} 不存在");
                continue;
            }

            if (existing.Code != source.Code)
                problems.Add($"活動 {source.Id} 的 Code 不符：{existing.Code} vs {source.Code}");

            for (var index = 0; index < source.Sections.Count; index++)
            {
                var s = source.Sections[index];
                var sectionId = source.PerformanceId * 10 + index + 1;

                if (!sections.TryGetValue(sectionId, out var section))
                {
                    problems.Add($"票區 {sectionId} 不存在");
                    continue;
                }

                if (section.PerformanceId != source.PerformanceId || section.Code != s.Code)
                    problems.Add($"票區 {sectionId} 的場次或代碼不符");
                if (section.RowCount != s.Rows || section.SeatsPerRow != s.SeatsPerRow)
                    problems.Add($"票區 {sectionId} 的幾何不符");

                var expectedSeats = s.Rows * s.SeatsPerRow;
                if (!seatCounts.TryGetValue(sectionId, out var actual) || actual != expectedSeats)
                    problems.Add($"票區 {sectionId} 的座位數不符：{(seatCounts.TryGetValue(sectionId, out var a) ? a : 0)} vs {expectedSeats}");
            }
        }

        if (problems.Count > 0)
            throw new InvalidOperationException(
                "既有 catalog 與 seed 檔不一致，已中止且未修改任何資料：" + Environment.NewLine
                + string.Join(Environment.NewLine, problems.Take(20)));
    }
}
