namespace Ticketing.Domain.Catalog;

/// <summary>
/// 座位的庫存操作。三個條件更新是**防超賣的核心**：
/// 它們回傳影響列數，呼叫端比對期望張數，不相等就整段 rollback（設計文件 06 第 4 節）。
/// 不能為了「Repository 不含規則」把 SQL 裡的狀態條件拿掉——
/// 那些條件正是原子性的來源，先查再無條件寫是錯的。
/// </summary>
public interface ISeatRepository
{
    Task<IReadOnlyList<Seat>> GetManyAsync(int performanceId, IReadOnlyCollection<int> seatIds, CancellationToken ct);

    /// <summary>連號配位用。過期的 Held 算可用。</summary>
    Task<IReadOnlyList<Seat>> GetAvailableInSectionAsync(int sectionId, DateTimeOffset now, CancellationToken ct);

    /// <summary>條件 UPDATE：只更新此刻仍可用的座位。回傳實際更新的列數。</summary>
    Task<int> TryHoldAsync(int performanceId, IReadOnlyCollection<int> seatIds, Guid holdId,
                           DateTimeOffset heldUntil, DateTimeOffset now, CancellationToken ct);

    /// <summary>條件 UPDATE：把屬於這個保留的座位全部改為 Sold。</summary>
    Task<int> MarkSoldAsync(Guid holdId, CancellationToken ct);

    /// <summary>條件 UPDATE：釋放屬於這個保留的座位。</summary>
    Task<int> ReleaseAsync(Guid holdId, CancellationToken ct);
}
