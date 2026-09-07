namespace Ticketing.Domain.Orders;

public interface IOrderRepository
{
    /// <summary>
    /// 僅供背景通知 worker 使用。
    /// **HTTP 路徑不得用它繞過買家篩選**——對外查詢一律走 <see cref="GetForBuyerAsync"/>。
    /// </summary>
    Task<Order?> GetForNotificationAsync(Guid orderId, CancellationToken ct);

    Task<Order?> GetForBuyerAsync(Guid orderId, Guid buyerId, CancellationToken ct);

    Task<Order?> GetByHoldAsync(Guid holdId, CancellationToken ct);

    /// <summary>每人上限用：這個買家在這一場已經付款的張數。</summary>
    Task<int> CountPaidSeatsAsync(Guid buyerId, int performanceId, CancellationToken ct);

    void Add(Order order);
}
