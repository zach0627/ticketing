using Ticketing.Application.Admin.Dtos;

namespace Ticketing.Application.Admin;

/// <summary>
/// 管理者的兩個寫入操作。
///
/// 兩個都要求 <c>Idempotency-Key</c>，但去重紀錄**不是**存在 <c>IdempotencyRecords</c>——
/// 那張表會被重置刪掉，於是「重置已完成但回覆遺失」的重試就失去判斷依據，
/// 會再刪一次剛產生的新訂單。所以管理操作的去重存在
/// **不被重置刪除的 `AdminAudits`**（設計文件 14 第 3 節）。
/// </summary>
public interface IAdminService
{
    Task<AdminOperationResponse> SetSalesPausedAsync(Guid actorId, int performanceId,
                                                     PauseSalesRequest request, Guid operationId,
                                                     CancellationToken ct);

    Task<AdminOperationResponse> ResetAsync(Guid actorId, ResetRequest request, Guid operationId,
                                            CancellationToken ct);
}

/// <summary>
/// 管理操作的結果。**沒有 HTTP 狀態欄位**：成功一律 200，
/// 所以稽核表不必存狀態碼（設計文件 14 第 3 節）。
/// </summary>
public sealed record AdminOperationResponse(string ResponseJson, bool IsReplay);
