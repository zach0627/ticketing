using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Api.Tests.Infrastructure;
using Ticketing.Infrastructure.Messaging;

namespace Ticketing.Api.Tests;

/// <summary>
/// 背景通知 worker（設計文件 06 第 9 節）。
///
/// 通知本身只寫一行 log，所以這裡驗的是**它有沒有在跑、會不會被一筆壞資料弄死**——
/// 那才是 `BackgroundService` 真正會出問題的地方。
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class NotificationWorkerTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task A_paid_order_is_queued_and_the_worker_drains_it()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.ClientFor(buyers[0]);

        var queue = factory.Services.GetRequiredService<ChannelOrderNotificationQueue>();

        var hold = await client.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)), BookingTestHelpers.NewKey());
        var order = await client.CheckoutAsync(await hold.HoldIdAsync(), "Succeeded", BookingTestHelpers.NewKey());

        Assert.Equal(HttpStatusCode.Created, order.StatusCode);

        // worker 是 BackgroundService，它會把佇列讀空。讀空代表它真的在跑，
        // 而且每一筆都拿到了自己的 DI scope（不然 Scoped 的 Repository 解析不出來）
        await WaitUntilDrainedAsync(queue);
    }

    [Fact]
    public async Task One_bad_item_does_not_kill_the_worker()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.ClientFor(buyers[0]);

        var queue = factory.Services.GetRequiredService<ChannelOrderNotificationQueue>();

        // 五筆不存在的訂單（管理者重置之後就是這個狀態）
        for (var i = 0; i < 5; i++) Assert.True(queue.TryEnqueue(Guid.NewGuid()));
        await WaitUntilDrainedAsync(queue);

        // worker 沒有死：後面真的訂單照樣被處理
        var hold = await client.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)), BookingTestHelpers.NewKey());
        await client.CheckoutAsync(await hold.HoldIdAsync(), "Succeeded", BookingTestHelpers.NewKey());

        await WaitUntilDrainedAsync(queue);
    }

    [Fact]
    public void The_queue_is_bounded_and_says_no_instead_of_growing_forever()
    {
        // 無界佇列在下游卡住時會把記憶體吃光——那比掉通知糟得多
        var queue = new ChannelOrderNotificationQueue();

        var accepted = Enumerable.Range(0, 150).Count(_ => queue.TryEnqueue(Guid.NewGuid()));

        Assert.Equal(100, accepted);                 // 上限 100
        Assert.False(queue.TryEnqueue(Guid.NewGuid()));   // 滿了就說滿了，不阻塞
    }

    private static async Task WaitUntilDrainedAsync(ChannelOrderNotificationQueue queue)
    {
        for (var attempt = 0; attempt < 100 && queue.Reader.Count > 0; attempt++)
            await Task.Delay(50);

        Assert.Equal(0, queue.Reader.Count);
    }
}
