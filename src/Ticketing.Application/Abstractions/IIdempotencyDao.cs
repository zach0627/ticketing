namespace Ticketing.Application.Abstractions;

/// <summary>
/// 冪等紀錄的存取。這是 **DAO 不是 Repository**：
/// <c>IdempotencyRecords</c> 不是業務概念、沒有行為、沒有聚合根，
/// 它只是一張表（設計文件 04 第 1 節、ADR-2）。
///
/// 介面在 Application 而不是 Domain，理由也是同一個：Domain 不認識它。
/// </summary>
public interface IIdempotencyDao
{
    Task<IdempotencyEntry?> FindAsync(Guid buyerId, string key, CancellationToken ct);

    /// <summary>只加入追蹤；真正寫入與業務結果**在同一個交易**一起 commit。</summary>
    void Add(Guid buyerId, string key, byte[] requestHash, int httpStatus, string responseJson,
             DateTimeOffset now);
}

public sealed record IdempotencyEntry(byte[] RequestHash, int HttpStatus, string ResponseJson);
