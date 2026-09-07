namespace Ticketing.Infrastructure.Persistence.Entities;

/// <summary>
/// 管理者做了什麼。**重置不會刪除這張表**——它保存管理操作的去重證據，
/// 否則 reset 已 commit 卻失去回覆時，重試會再次清掉新一輪的訂單（設計文件 14）。
/// </summary>
public sealed class AdminAudit
{
    public long Id { get; set; }                        // 只有這張表用 identity
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string? DetailsJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>非 NULL 時唯一。以下三個欄位必須同時存在或同時為 NULL。</summary>
    public Guid? OperationId { get; set; }
    public byte[]? RequestHash { get; set; }
    public string? ResultJson { get; set; }
}
