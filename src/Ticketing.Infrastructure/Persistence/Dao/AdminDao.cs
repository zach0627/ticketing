using Microsoft.EntityFrameworkCore;
using Ticketing.Application.Abstractions;
using Ticketing.Domain.Catalog;
using Ticketing.Infrastructure.Persistence.Entities;

namespace Ticketing.Infrastructure.Persistence.Dao;

/// <summary>
/// 管理操作的批次資料存取。
///
/// 這裡的方法**都是整張表的操作**，所以不走 Repository：
/// 沒有聚合根、沒有 Domain 行為，就是「對這些表做這件事」（設計文件 14 第 2 節）。
/// 交易邊界由 <c>AdminService</c> 決定，這裡一律不 commit。
/// </summary>
public sealed class AdminDao(TicketingDbContext db) : IAdminDao
{
    public Task<AdminOperationRecord?> FindOperationAsync(Guid operationId, CancellationToken ct)
        => db.AdminAudits.AsNoTracking()
             .Where(a => a.OperationId == operationId)
             .Select(a => new AdminOperationRecord(a.ActorId, a.Action, a.RequestHash!, a.ResultJson!))
             .FirstOrDefaultAsync(ct);

    public async Task<PurchaseCounts> CountPurchasesAsync(CancellationToken ct)
        => new(await db.Orders.CountAsync(ct), await db.SeatHolds.CountAsync(ct));

    /// <summary>
    /// 一句 UPDATE 同時改三個欄位——少改一個就會被 <c>CK_Seats_State</c> 擋下來，
    /// 所以這條約束也順便保護了這裡（設計文件 04 第 4 節）。
    /// </summary>
    public Task<int> ReleaseAllSeatsAsync(CancellationToken ct)
        => db.Seats
             .Where(s => s.Status != SeatStatus.Available)
             .ExecuteUpdateAsync(set => set
                 .SetProperty(s => s.Status, SeatStatus.Available)
                 .SetProperty(s => s.HoldId, (Guid?)null)
                 .SetProperty(s => s.HeldUntilUtc, (DateTimeOffset?)null), ct);

    /// <summary>
    /// 依外鍵順序刪五張表。**不靠 cascade**——我們要明確知道刪了什麼、順序是什麼。
    ///
    /// 這裡用原生 SQL 而不是 EF：owned 集合（OrderItems／SeatHoldItems）沒有自己的 DbSet，
    /// 而這本來就是「對整張表做一件事」，正是 DAO 存在的理由。
    /// 語句沒有任何參數，也沒有字串拼接。
    /// </summary>
    public Task DeleteAllPurchaseDataAsync(CancellationToken ct)
        => db.Database.ExecuteSqlRawAsync("""
            DELETE FROM dbo.OrderItems;
            DELETE FROM dbo.Orders;
            DELETE FROM dbo.SeatHoldItems;
            DELETE FROM dbo.SeatHolds;
            DELETE FROM dbo.IdempotencyRecords;
            """, ct);

    public async Task<DateTimeOffset?> GetEarliestPerformanceStartAsync(CancellationToken ct)
        => await db.Performances.AsNoTracking()
                   .OrderBy(p => p.StartsAtUtc)
                   .Select(p => (DateTimeOffset?)p.StartsAtUtc)
                   .FirstOrDefaultAsync(ct);

    /// <summary>
    /// <c>AddDays</c> 會翻成 SQL 的 <c>DATEADD(day, …)</c>，整批更新不必載入實體。
    /// 平移後仍要滿足 <c>CK_Performances_Times</c>（開賣 &lt; 停售 &lt; 開演）。
    /// </summary>
    public Task ResetPerformanceScheduleAsync(int shiftDays, DateTimeOffset salesOpensAtUtc,
                                              CancellationToken ct)
        => db.Performances.ExecuteUpdateAsync(set => set
                 .SetProperty(p => p.StartsAtUtc, p => p.StartsAtUtc.AddDays(shiftDays))
                 .SetProperty(p => p.SalesClosesAtUtc, p => p.SalesClosesAtUtc.AddDays(shiftDays))
                 .SetProperty(p => p.SalesOpensAtUtc, salesOpensAtUtc)
                 .SetProperty(p => p.IsSalesPaused, false), ct);

    public void AddAudit(Guid actorId, string action, Guid operationId, byte[] requestHash,
                         string? detailsJson, string resultJson, DateTimeOffset now)
        => db.AdminAudits.Add(new AdminAudit
        {
            ActorId = actorId,
            Action = action,
            OperationId = operationId,
            RequestHash = requestHash,
            DetailsJson = detailsJson,
            ResultJson = resultJson,
            CreatedAtUtc = now
        });
}
