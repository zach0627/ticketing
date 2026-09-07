using Microsoft.EntityFrameworkCore;
using Ticketing.Domain.Orders;

namespace Ticketing.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository(TicketingDbContext db) : IOrderRepository
{
    /// <summary>僅供背景通知 worker。HTTP 路徑不得用它繞過買家篩選。</summary>
    public Task<Order?> GetForNotificationAsync(Guid orderId, CancellationToken ct)
        => db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct);

    public Task<Order?> GetForBuyerAsync(Guid orderId, Guid buyerId, CancellationToken ct)
        => db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.BuyerId == buyerId, ct);

    public Task<Order?> GetByHoldAsync(Guid holdId, CancellationToken ct)
        => db.Orders.FirstOrDefaultAsync(o => o.HoldId == holdId, ct);

    /// <summary>每人上限用。走 IX_Orders_BuyerPerformance。</summary>
    public async Task<int> CountPaidSeatsAsync(Guid buyerId, int performanceId, CancellationToken ct)
        => await db.Orders.AsNoTracking()
                   .Where(o => o.BuyerId == buyerId && o.PerformanceId == performanceId)
                   .SelectMany(o => EF.Property<IReadOnlyList<OrderItem>>(o, "_items"))
                   .CountAsync(ct);

    public void Add(Order order) => db.Orders.Add(order);
}
