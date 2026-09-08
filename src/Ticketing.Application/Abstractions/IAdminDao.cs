namespace Ticketing.Application.Abstractions;

/// <summary>
/// 管理操作的批次資料存取。**DAO 不是 Repository**：
/// 重置一次橫跨五張表，沒有聚合根可言；步驟與交易邊界由 <c>AdminService</c> 決定，
/// 這裡只負責「把這件事對資料庫做出來」（設計文件 14 第 2 節、ADR-2）。
///
/// 統計查詢不在這裡——那是 <c>IAdminQueries</c> 的事，不要兩邊各放一套。
/// </summary>
public interface IAdminDao
{
    /// <summary>查管理操作的去重紀錄。<c>AdminAudits</c> **不會被重置刪除**，所以重播判斷才可靠。</summary>
    Task<AdminOperationRecord?> FindOperationAsync(Guid operationId, CancellationToken ct);

    /// <summary>取得所有場次 gate 之後才能數，否則數到一半還有人在買。</summary>
    Task<PurchaseCounts> CountPurchasesAsync(CancellationToken ct);

    /// <summary>非 Available 的座位全部改回 Available，同句清掉 HoldId 與 HeldUntilUtc。回真正改動的席數。</summary>
    Task<int> ReleaseAllSeatsAsync(CancellationToken ct);

    /// <summary>依外鍵順序清掉五張購買資料表。**不靠 cascade**。</summary>
    Task DeleteAllPurchaseDataAsync(CancellationToken ct);

    Task<DateTimeOffset?> GetEarliestPerformanceStartAsync(CancellationToken ct);

    /// <summary>開演與停售各平移 <paramref name="shiftDays"/> 天，開賣設成傳入值，並解除所有暫停。</summary>
    Task ResetPerformanceScheduleAsync(int shiftDays, DateTimeOffset salesOpensAtUtc, CancellationToken ct);

    /// <summary>只加入追蹤；**不自己 commit**——稽核與業務結果必須在同一個交易。</summary>
    void AddAudit(Guid actorId, string action, Guid operationId, byte[] requestHash,
                  string? detailsJson, string resultJson, DateTimeOffset now);
}

public sealed record AdminOperationRecord(Guid ActorId, string Action, byte[] RequestHash, string ResultJson);

public sealed record PurchaseCounts(int Orders, int Holds);
