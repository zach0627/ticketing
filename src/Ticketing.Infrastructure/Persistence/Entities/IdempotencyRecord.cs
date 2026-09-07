namespace Ticketing.Infrastructure.Persistence.Entities;

/// <summary>
/// 同一個 Idempotency-Key 上一次的回應。**不是業務概念**，所以放在 Infrastructure，
/// 存取介面叫 DAO（設計文件 04 第 1 節）。
/// 主鍵 (BuyerId, Key) 是冪等機制的最終保證：同 key 只能寫一筆。
/// </summary>
public sealed class IdempotencyRecord
{
    public Guid BuyerId { get; set; }
    public string Key { get; set; } = "";

    /// <summary>完整 fingerprint 的雜湊：版本＋method＋路由 ID＋正規化 body（設計文件 06 第 7 節）。</summary>
    public byte[] RequestHash { get; set; } = [];

    public int HttpStatus { get; set; }
    public string ResponseJson { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
