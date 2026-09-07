using Microsoft.EntityFrameworkCore;
using Ticketing.Domain.Catalog;

namespace Ticketing.Infrastructure.Persistence.Repositories;

/// <summary>
/// ⭐ 防超賣的核心。三個條件更新用 <c>ExecuteUpdateAsync</c>：
/// WHERE 裡的狀態條件**就是原子性的來源**，不能為了「Repository 不含規則」拿掉，
/// 也不能改成先查再無條件寫（設計文件 06 第 4.1 節、12 第 3 節）。
///
/// <c>ExecuteUpdate</c> 不更新 ChangeTracker，所以這裡一律不載入 tracked 的 Seat。
/// </summary>
public sealed class SeatRepository(TicketingDbContext db) : ISeatRepository
{
    public async Task<IReadOnlyList<Seat>> GetManyAsync(int performanceId,
        IReadOnlyCollection<int> seatIds, CancellationToken ct)
        => await db.Seats.AsNoTracking()
                   .Where(s => s.PerformanceId == performanceId && seatIds.Contains(s.Id))
                   .ToListAsync(ct);

    /// <summary>可用 ＝ Available，或 Held 但保留已到期（沒有背景清理程式）。</summary>
    public async Task<IReadOnlyList<Seat>> GetAvailableInSectionAsync(int sectionId,
        DateTimeOffset now, CancellationToken ct)
        => await db.Seats.AsNoTracking()
                   .Where(s => s.SectionId == sectionId
                            && (s.Status == SeatStatus.Available
                             || (s.Status == SeatStatus.Held && s.HeldUntilUtc <= now)))
                   .OrderBy(s => s.RowNumber).ThenBy(s => s.SeatNumber)
                   .ToListAsync(ct);

    /// <summary>
    /// 只拿走此刻仍可用的座位。回傳影響列數——呼叫端必須比對它等於相異席數，
    /// 少一列就整段 rollback。兩人同搶一席時只有一個交易改得動。
    /// </summary>
    public Task<int> TryHoldAsync(int performanceId, IReadOnlyCollection<int> seatIds, Guid holdId,
                                  DateTimeOffset heldUntil, DateTimeOffset now, CancellationToken ct)
        => db.Seats
             .Where(s => s.PerformanceId == performanceId
                      && seatIds.Contains(s.Id)
                      && (s.Status == SeatStatus.Available
                       || (s.Status == SeatStatus.Held && s.HeldUntilUtc <= now)))
             .ExecuteUpdateAsync(set => set
                 .SetProperty(s => s.Status, SeatStatus.Held)
                 .SetProperty(s => s.HoldId, holdId)
                 .SetProperty(s => s.HeldUntilUtc, heldUntil), ct);

    /// <summary>只改仍屬於這筆保留的 Held 座位。付款時影響列數不足也要 rollback。</summary>
    public Task<int> MarkSoldAsync(Guid holdId, CancellationToken ct)
        => db.Seats
             .Where(s => s.HoldId == holdId && s.Status == SeatStatus.Held)
             .ExecuteUpdateAsync(set => set
                 .SetProperty(s => s.Status, SeatStatus.Sold)
                 .SetProperty(s => s.HeldUntilUtc, (DateTimeOffset?)null), ct);

    /// <summary>
    /// 釋放。影響 0 列是合法的——過期的座位可能已經被別人拿走，
    /// 這個 WHERE 保證不會誤放別人的保留，也不碰 Sold。
    /// </summary>
    public Task<int> ReleaseAsync(Guid holdId, CancellationToken ct)
        => db.Seats
             .Where(s => s.HoldId == holdId && s.Status == SeatStatus.Held)
             .ExecuteUpdateAsync(set => set
                 .SetProperty(s => s.Status, SeatStatus.Available)
                 .SetProperty(s => s.HoldId, (Guid?)null)
                 .SetProperty(s => s.HeldUntilUtc, (DateTimeOffset?)null), ct);
}
