using Microsoft.EntityFrameworkCore;
using Ticketing.Application.Catalog;
using Ticketing.Application.Catalog.Dtos;
using Ticketing.Application.Common;
using Ticketing.Domain.Booking;
using Ticketing.Domain.Catalog;

namespace Ticketing.Infrastructure.Persistence.Queries;

/// <summary>
/// 唯讀查詢，直接投影成 DTO，不載入聚合（ADR-5 讀寫分離）。
///
/// 三個共同規則：
/// ① 一律 <c>AsNoTracking</c>——這些資料不會被寫回，不需要 ChangeTracker 的開銷。
/// ② 只投影 DTO 需要的欄位，**禁止先 ToList 再逐筆查關聯**（那就是 N+1）。
/// ③ 需要 <c>now</c> 的欄位（售票狀態、座位可用性）在投影**之後**用 Domain 的靜態
///    純函式計算——不在 SQL 裡複製一份規則，那會漂移。
/// </summary>
public sealed class CatalogQueries(TicketingDbContext db) : ICatalogQueries
{
    private const string Currency = "TWD";

    /// <summary>
    /// 票區投影。**必須是頂層查詢**：Mapperly 的 <c>ProjectToSections</c> 接受
    /// <c>IQueryable&lt;Section&gt;</c>，巢狀在另一個 <c>Select</c> 裡時 EF 會把內層集合
    /// 變成 <c>List&lt;Section&gt;</c>，簽章對不上而在執行期擲 <c>ArgumentException</c>。
    /// 拆成獨立查詢：多一次往返，但每一句都簡單、可翻譯，而且不是 N+1（固定兩句）。
    /// </summary>
    private async Task<IReadOnlyList<SectionDto>> SectionsOfAsync(int performanceId, CancellationToken ct)
        => await db.Sections.AsNoTracking()
                   .Where(s => s.PerformanceId == performanceId)
                   .OrderBy(s => s.Code)
                   .ProjectToSections()
                   .ToListAsync(ct);

    public async Task<PagedResult<EventCardDto>> GetEventCardsAsync(
        EventCategory? category, int page, int pageSize, DateTimeOffset now, CancellationToken ct)
    {
        var query = db.Performances.AsNoTracking().Where(p => p.Event.IsPublished);

        if (category is { } wanted)
            query = query.Where(p => p.Event.Category == wanted);

        var total = await query.CountAsync(ct);

        // 只取這一頁需要的欄位。p.Event 會被翻成 JOIN，不是第二次查詢。
        var rows = await query
            .OrderBy(p => p.StartsAtUtc).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                EventId = p.Event.Id,
                p.Event.Code,
                p.Event.Category,
                p.Event.Title,
                p.Event.Performer,
                p.Event.ImagePath,
                p.City,
                PerformanceId = p.Id,
                p.StartsAtUtc,
                p.IsSalesPaused,
                p.SalesOpensAtUtc,
                p.SalesClosesAtUtc,
                MinPrice = db.Sections.Where(s => s.PerformanceId == p.Id).Min(s => s.Price)
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new EventCardDto(
            r.EventId, r.Code, r.Category, r.Title, r.Performer, r.ImagePath, r.City,
            r.PerformanceId, r.StartsAtUtc, r.MinPrice, Currency,
            Performance.SalesStatusAt(r.IsSalesPaused, r.SalesOpensAtUtc, r.SalesClosesAtUtc, now),
            now)).ToList();

        return new PagedResult<EventCardDto>(items, total);
    }

    public async Task<EventDetailDto?> GetEventDetailAsync(string code, DateTimeOffset now, CancellationToken ct)
    {
        var row = await db.Performances.AsNoTracking()
            .Where(p => p.Event.Code == code && p.Event.IsPublished)
            .Select(p => new
            {
                EventId = p.Event.Id,
                p.Event.Code,
                p.Event.Category,
                p.Event.Title,
                p.Event.Performer,
                p.Event.Genre,
                p.Event.Description,
                p.Event.ImagePath,
                PerformanceId = p.Id,
                p.City,
                p.Venue,
                p.StartsAtUtc,
                p.SalesOpensAtUtc,
                p.SalesClosesAtUtc,
                p.DurationMinutes,
                p.IsSalesPaused
            })
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;

        var sections = await SectionsOfAsync(row.PerformanceId, ct);

        // 每人上限與能否連號來自 Domain 的政策，不存資料表（ADR：避免兩個真相）
        var policy = PurchasePolicy.For(row.Category);
        var salesStatus = Performance.SalesStatusAt(row.IsSalesPaused, row.SalesOpensAtUtc, row.SalesClosesAtUtc, now);

        return new EventDetailDto(
            row.EventId, row.Code, row.Category, row.Title, row.Performer, row.Genre,
            row.Description, row.ImagePath,
            new PerformanceSummaryDto(row.PerformanceId, row.City, row.Venue, row.StartsAtUtc,
                                      row.SalesOpensAtUtc, row.SalesClosesAtUtc, row.DurationMinutes,
                                      row.IsSalesPaused, salesStatus),
            sections,
            policy.MaxTicketsPerBuyer,
            policy.AllowsContiguousAllocation,
            Currency,
            now);
    }

    public async Task<SeatMapDto?> GetSeatMapAsync(int performanceId, DateTimeOffset now, CancellationToken ct)
    {
        var header = await db.Performances.AsNoTracking()
            .Where(p => p.Id == performanceId && p.Event.IsPublished)
            .Select(p => new
            {
                p.Event.Code,
                p.Event.Title,
                p.Event.Category,
                p.IsSalesPaused,
                p.SalesOpensAtUtc,
                p.SalesClosesAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (header is null) return null;

        var sections = await SectionsOfAsync(performanceId, ct);

        // 座位只取五個欄位。**不取 HoldId、不取買家**——那是別人的資料，公開端點不得外洩。
        var rawSeats = await db.Seats.AsNoTracking()
            .Where(s => s.PerformanceId == performanceId)
            .OrderBy(s => s.SectionId).ThenBy(s => s.RowNumber).ThenBy(s => s.SeatNumber)
            .Select(s => new { s.Id, s.SectionId, s.RowNumber, s.SeatNumber, s.Status, s.HeldUntilUtc })
            .ToListAsync(ct);

        var seats = rawSeats.Select(s => new SeatDto(
            s.Id, s.SectionId, s.RowNumber, s.SeatNumber,
            // 過期的 Held 對外顯示成 Available——與 Seat.IsAvailableAt 是同一段程式碼
            Seat.IsAvailableAt(s.Status, s.HeldUntilUtc, now) ? SeatStatus.Available : s.Status))
            .ToList();

        var policy = PurchasePolicy.For(header.Category);

        return new SeatMapDto(
            performanceId, header.Code, header.Title, header.Category,
            policy.MaxTicketsPerBuyer, policy.AllowsContiguousAllocation,
            Performance.SalesStatusAt(header.IsSalesPaused, header.SalesOpensAtUtc, header.SalesClosesAtUtc, now),
            now, sections, seats);
    }
}
