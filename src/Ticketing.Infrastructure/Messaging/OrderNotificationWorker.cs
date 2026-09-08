using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ticketing.Domain.Orders;

namespace Ticketing.Infrastructure.Messaging;

/// <summary>
/// 把佇列裡的訂單拿出來「送通知」——本專案只寫一行 log，因為不寄真的信。
///
/// 兩個 <c>BackgroundService</c> 的必修題：
///
/// ① **每一筆都要自己的 DI scope**。<c>BackgroundService</c> 是 Singleton，
///    不能直接注入 Scoped 的 Repository（那會讓一個 <c>DbContext</c> 活到程序結束，
///    而且多筆通知共用同一個追蹤狀態）。所以注入 <see cref="IServiceScopeFactory"/>，
///    每筆建一個 scope。
///
/// ② **單筆失敗不能弄倒整個 worker**。沒有 catch 的話，一筆壞資料就讓
///    後面所有通知都不會被處理，而且沒有人會發現——背景服務死掉是安靜的。
/// </summary>
public sealed class OrderNotificationWorker(
    ChannelOrderNotificationQueue queue,
    IServiceScopeFactory scopes,
    ILogger<OrderNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var orderId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await NotifyAsync(orderId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;                     // 關機，不是錯誤
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "NotificationFailed {OrderId}", orderId);
            }
        }
    }

    private async Task NotifyAsync(Guid orderId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        var order = await orders.GetForNotificationAsync(orderId, ct);
        if (order is null)
        {
            // 管理者重置會刪掉訂單，這時候通知就沒有意義了——略過，不是錯誤
            logger.LogInformation("NotificationSkipped {OrderId}", orderId);
            return;
        }

        logger.LogInformation("MockNotificationSent {OrderId} {Tickets} {Amount}",
                              order.Id, order.Items.Count, order.TotalAmount);
    }
}
