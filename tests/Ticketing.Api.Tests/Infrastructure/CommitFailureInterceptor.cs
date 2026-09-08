using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ticketing.Api.Tests.Infrastructure;

/// <summary>
/// 在交易 commit 的前後注入一次故障，用來測 T21 的兩種邊界。
///
/// <list type="bullet">
/// <item><b>commit 之後</b>丟例外＝「資料已經寫進去了，但客戶端沒收到回應」。
///       客戶端唯一能做的是拿同一個 key 重送——**必須拿到原本那張訂單，不是第二張**。</item>
/// <item><b>commit 之前</b>丟例外＝整段 rollback。這時**不可以**回假成功，
///       資料庫也不能留下任何一半的東西。</item>
/// </list>
/// </summary>
public sealed class CommitFailureInterceptor : DbTransactionInterceptor
{
    private int _failAfterCommit;
    private int _failBeforeCommit;

    public void FailOnceAfterCommit() => Interlocked.Exchange(ref _failAfterCommit, 1);

    public void FailOnceBeforeCommit() => Interlocked.Exchange(ref _failBeforeCommit, 1);

    // ⚠️ 一定要覆寫 *Async 版本。EF 的 `CommitAsync()` 只會呼叫 async 鉤子，
    // 只覆寫同步版的話攔截器看起來「沒有作用」——這是第一版真的踩到的坑。
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _failBeforeCommit, 0) == 1)
            throw new InvalidOperationException("測試注入：commit 之前失敗");

        return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
    }

    public override Task TransactionCommittedAsync(DbTransaction transaction,
        TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _failAfterCommit, 0) == 1)
            throw new InvalidOperationException("測試注入：commit 成功但回覆遺失");

        return base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
    }
}
