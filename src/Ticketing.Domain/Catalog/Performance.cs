using Ticketing.Domain.Common;

namespace Ticketing.Domain.Catalog;

/// <summary>
/// 活動的一次演出：時間、場館、售票窗口。
/// 售票狀態是**算出來的**，不存欄位；時間一律由外部傳入，Domain 不讀系統時鐘
/// （設計文件 04 第 2.4 節）。
/// </summary>
public sealed class Performance
{
    public int Id { get; private set; }
    public int EventId { get; private set; }
    public string Venue { get; private set; } = "";
    public string City { get; private set; } = "";
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset SalesOpensAtUtc { get; private set; }
    public DateTimeOffset SalesClosesAtUtc { get; private set; }
    public int DurationMinutes { get; private set; }
    public bool IsSalesPaused { get; private set; }

    /// <summary>導覽屬性。查詢時以 Include 帶入；<see cref="PurchasePolicy"/> 需要它的 Category。</summary>
    public Event Event { get; private set; } = null!;

    private Performance() { }                               // EF Core 用

    public Performance(int id, Event @event, string venue, string city,
                       DateTimeOffset startsAtUtc, DateTimeOffset salesOpensAtUtc,
                       DateTimeOffset salesClosesAtUtc, int durationMinutes)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (id <= 0 || durationMinutes <= 0)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "場次的 Id 與時長必須大於 0");

        // 與資料庫的 CK_Performances_Times 是同一條規則，兩邊都要有：
        // 這裡回看得懂的錯誤，資料庫負責就算程式漏查也寫不進去。
        if (salesOpensAtUtc >= salesClosesAtUtc || salesClosesAtUtc >= startsAtUtc)
            throw new BookingRuleException(ErrorCode.ValidationFailed,
                "售票時間必須是「開賣 < 停售 < 開演」");

        Id = id;
        Event = @event;
        EventId = @event.Id;
        Venue = venue;
        City = city;
        StartsAtUtc = startsAtUtc;
        SalesOpensAtUtc = salesOpensAtUtc;
        SalesClosesAtUtc = salesClosesAtUtc;
        DurationMinutes = durationMinutes;
        IsSalesPaused = false;
    }

    /// <summary>管理者暫停優先於時間窗口：暫停中一律回 Paused。</summary>
    public SalesStatus SalesStatusAt(DateTimeOffset now)
        => SalesStatusAt(IsSalesPaused, SalesOpensAtUtc, SalesClosesAtUtc, now);

    /// <summary>
    /// 同一條規則的靜態版本，給**查詢投影**用。
    ///
    /// 唯讀查詢不載入 Performance 實體（那會白抓一堆欄位），只投影需要的三個欄位；
    /// 但售票狀態的判斷不該因此被複製一份到 Queries 裡——複製就會漂移。
    /// 抽成靜態純函式後，實體方法與查詢投影**呼叫的是同一段程式碼**。
    /// </summary>
    public static SalesStatus SalesStatusAt(bool isSalesPaused, DateTimeOffset salesOpensAtUtc,
                                            DateTimeOffset salesClosesAtUtc, DateTimeOffset now)
    {
        if (isSalesPaused) return SalesStatus.Paused;
        if (now < salesOpensAtUtc) return SalesStatus.NotYetOnSale;
        if (now >= salesClosesAtUtc) return SalesStatus.SalesClosed;
        return SalesStatus.OnSale;
    }

    public void PauseSales() => IsSalesPaused = true;

    public void ResumeSales() => IsSalesPaused = false;

    /// <summary>重置時整批平移日期（設計文件 14）。開賣時間另外指定，不跟著平移。</summary>
    public void Reschedule(TimeSpan shift, DateTimeOffset newSalesOpensAtUtc)
    {
        var startsAt = StartsAtUtc + shift;
        var salesClosesAt = SalesClosesAtUtc + shift;

        if (newSalesOpensAtUtc >= salesClosesAt || salesClosesAt >= startsAt)
            throw new BookingRuleException(ErrorCode.ValidationFailed,
                "平移後的售票時間必須仍是「開賣 < 停售 < 開演」");

        StartsAtUtc = startsAt;
        SalesClosesAtUtc = salesClosesAt;
        SalesOpensAtUtc = newSalesOpensAtUtc;
    }
}
