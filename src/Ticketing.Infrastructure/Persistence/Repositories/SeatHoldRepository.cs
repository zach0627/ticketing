using Microsoft.EntityFrameworkCore;
using Ticketing.Domain.Booking;

namespace Ticketing.Infrastructure.Persistence.Repositories;

public sealed class SeatHoldRepository(TicketingDbContext db) : ISeatHoldRepository
{
    /// <summary>只用來定位要鎖哪一場。取得 gate 後必須重新載入，這裡刻意 no-tracking。</summary>
    public async Task<int?> FindPerformanceForBuyerAsync(Guid holdId, Guid buyerId, CancellationToken ct)
        => await db.SeatHolds.AsNoTracking()
                   .Where(h => h.Id == holdId && h.BuyerId == buyerId)
                   .Select(h => (int?)h.PerformanceId)
                   .FirstOrDefaultAsync(ct);

    /// <summary>含 Items。別人的保留回 null，Api 轉 404，不透露它存在。</summary>
    public Task<SeatHold?> GetForBuyerAsync(Guid holdId, Guid buyerId, CancellationToken ct)
        => db.SeatHolds
             .FirstOrDefaultAsync(h => h.Id == holdId && h.BuyerId == buyerId, ct);

    /// <summary>資料庫狀態為 Active，**包含已到期但尚未整理的**——時間到不會自動退出唯一索引。</summary>
    public Task<SeatHold?> FindActiveAsync(Guid buyerId, int performanceId, CancellationToken ct)
        => db.SeatHolds
             .FirstOrDefaultAsync(h => h.BuyerId == buyerId
                                    && h.PerformanceId == performanceId
                                    && h.Status == HoldStatus.Active, ct);

    public void Add(SeatHold hold) => db.SeatHolds.Add(hold);
}
