using Microsoft.EntityFrameworkCore;
using Ticketing.Application.Abstractions;
using Ticketing.Infrastructure.Persistence.Entities;

namespace Ticketing.Infrastructure.Persistence.Dao;

/// <summary>
/// 冪等紀錄的存取。**DAO 不是 Repository**：沒有聚合、沒有行為，就是一張表。
///
/// <c>Add</c> 只加入追蹤，真正寫入交給呼叫端的 <c>SaveChangesAsync</c>——
/// 紀錄必須和業務結果在**同一個交易**裡 commit，
/// 否則會出現「訂單成立了但沒有紀錄」或反過來（設計文件 06 第 7 節）。
/// </summary>
public sealed class IdempotencyDao(TicketingDbContext db) : IIdempotencyDao
{
    public Task<IdempotencyEntry?> FindAsync(Guid buyerId, string key, CancellationToken ct)
        => db.IdempotencyRecords.AsNoTracking()
             .Where(r => r.BuyerId == buyerId && r.Key == key)
             .Select(r => new IdempotencyEntry(r.RequestHash, r.HttpStatus, r.ResponseJson))
             .FirstOrDefaultAsync(ct);

    public void Add(Guid buyerId, string key, byte[] requestHash, int httpStatus, string responseJson,
                    DateTimeOffset now)
        => db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            BuyerId = buyerId,
            Key = key,
            RequestHash = requestHash,
            HttpStatus = httpStatus,
            ResponseJson = responseJson,
            CreatedAtUtc = now
        });
}
