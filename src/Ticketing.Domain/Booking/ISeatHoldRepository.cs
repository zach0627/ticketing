namespace Ticketing.Domain.Booking;

public interface ISeatHoldRepository
{
    /// <summary>
    /// 只用來定位「要鎖哪一場」的 no-tracking 查詢。
    /// 取得 gate 之後**必須重新載入**——期間管理重置可能已經把它刪掉（設計文件 06 第 3.2 節）。
    /// </summary>
    Task<int?> FindPerformanceForBuyerAsync(Guid holdId, Guid buyerId, CancellationToken ct);

    /// <summary>含 Items。別人的保留回 null，由 Api 轉成 404（不透露它存在）。</summary>
    Task<SeatHold?> GetForBuyerAsync(Guid holdId, Guid buyerId, CancellationToken ct);

    /// <summary>查資料庫狀態為 Active 的保留，**包含已到期但還沒被整理的**。</summary>
    Task<SeatHold?> FindActiveAsync(Guid buyerId, int performanceId, CancellationToken ct);

    void Add(SeatHold hold);
}
