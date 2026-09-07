using Microsoft.EntityFrameworkCore;
using Ticketing.Domain.Common;

namespace Ticketing.Infrastructure.Persistence;

/// <summary>
/// 交易邊界 ＋ 暫時性錯誤重試。
/// <c>EnableRetryOnFailure</c> 使暫時性 SQL 錯誤與死結可以重跑**整段**；
/// 因此委派外只能捕捉不變的輸入與操作 key，不能捕捉上一輪的 tracked entity 或 now
/// （設計文件 06 第 3 節）。
/// </summary>
public sealed class UnitOfWork(TicketingDbContext db) : IUnitOfWork
{
    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);

        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();                       // 重試時不能帶著上一輪的追蹤狀態
            await using var tx = await db.Database.BeginTransactionAsync(ct);   // READ COMMITTED
            var result = await body(ct);
            await tx.CommitAsync(ct);
            return result;
        });
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
