namespace Ticketing.Domain.Common;

/// <summary>
/// 交易邊界。Application 看不到 EF，所以需要這個介面來表達「整段在一個交易裡跑，失敗整段退回」。
/// 實作在 Infrastructure（ExecutionStrategy ＋ BeginTransaction ＋ Commit，設計文件 06 第 3 節）。
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// 以重試策略包住整段交易。<paramref name="body"/> 可能被重跑，
    /// 所以委派外只能捕捉不變的輸入，不能捕捉上一輪的 tracked entity 或 now。
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> body, CancellationToken ct);

    /// <summary>把追蹤中的變更寫入資料庫。仍在交易內，尚未 commit。</summary>
    Task<int> SaveChangesAsync(CancellationToken ct);
}
