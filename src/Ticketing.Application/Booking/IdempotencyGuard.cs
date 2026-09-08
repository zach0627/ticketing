using Ticketing.Application.Abstractions;
using Ticketing.Domain.Common;

namespace Ticketing.Application.Booking;

/// <summary>
/// 冪等的判斷邏輯。**刻意是具體類別，沒有介面**——
/// 它沒有第二種實作，也不需要被替換；要被替換的是它底下的 <see cref="IIdempotencyDao"/>
/// （設計文件 06 第 7 節、12 第 3 節「不建無理由的抽象」）。
///
/// 查詢與保存都在**買家 gate 之內**，所以同一個 key 的並行請求會排隊：
/// 後到的那個直接讀到前一個寫下的紀錄，不會兩個都去搶座位。
/// 唯一索引 <c>(BuyerId, Key)</c> 仍是最後防線。
/// </summary>
public sealed class IdempotencyGuard(IIdempotencyDao dao)
{
    public async Task<IdempotencyResponse?> FindReplayAsync(Guid buyerId, string key, byte[] fingerprint,
                                                            CancellationToken ct)
    {
        var existing = await dao.FindAsync(buyerId, key, ct);
        if (existing is null) return null;

        // 同一個 key 用在不同的內容 → 409。這是使用者端的錯誤，不是我們該猜的事：
        // 「大概是想重送吧」然後回上一次的結果，可能讓他拿到完全不同場次的訂單。
        if (!existing.RequestHash.AsSpan().SequenceEqual(fingerprint))
            throw new BookingRuleException(ErrorCode.IdempotencyKeyReuse,
                                           "相同的 Idempotency-Key 用在不同的請求內容");

        return new IdempotencyResponse(existing.HttpStatus, existing.ResponseJson, IsReplay: true);
    }

    public void Save(Guid buyerId, string key, byte[] fingerprint, int httpStatus, string responseJson,
                     DateTimeOffset now)
        => dao.Add(buyerId, key, fingerprint, httpStatus, responseJson, now);
}
