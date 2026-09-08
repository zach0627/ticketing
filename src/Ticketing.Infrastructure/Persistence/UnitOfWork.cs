using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Ticketing.Application.Abstractions;
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
    // 唯一索引違反（2627）與唯一鍵重複（2601）。SQL Server 兩個都會用，
    // 取決於索引是 constraint 還是 index，所以兩個都要認。
    private const int UniqueConstraintViolation = 2627;
    private const int DuplicateKeyRow = 2601;

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

    public async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IdentityConflictIn(ex) is { } conflict)
        {
            // 失敗的追蹤狀態一定要清掉：不清的話，接下來 AuthService 為了解衝突而做的
            // 重新查詢，會從 ChangeTracker 拿回那個「加到一半」的實體，看到假的結果。
            db.ChangeTracker.Clear();

            throw new UserIdentityConflictException(conflict, ex);
        }
    }

    /// <summary>
    /// 只翻譯**我們自己命名的兩個索引**，其他唯一索引衝突原樣往上（最後變成 500）。
    ///
    /// 靠訊息比對索引名稱不漂亮，但 SQL Server 沒有提供結構化的索引欄位；
    /// 能這樣做的前提是索引名稱由我們在 <c>AppUserConfiguration</c> 明確指定，
    /// 不是 EF 自動產生的（設計文件 13 第 3 節）。
    /// </summary>
    private static UserIdentityConflict? IdentityConflictIn(DbUpdateException exception)
    {
        if (exception.InnerException is not SqlException
            { Number: UniqueConstraintViolation or DuplicateKeyRow } sql)
            return null;

        if (sql.Message.Contains("UQ_AppUsers_Email", StringComparison.Ordinal))
            return UserIdentityConflict.Email;

        if (sql.Message.Contains("UX_AppUsers_GoogleSubject", StringComparison.Ordinal))
            return UserIdentityConflict.GoogleSubject;

        return null;
    }
}
