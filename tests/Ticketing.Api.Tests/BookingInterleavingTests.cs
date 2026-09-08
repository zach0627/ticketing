using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Ticketing.Api.Tests.Infrastructure;

namespace Ticketing.Api.Tests;

/// <summary>
/// T15～T17、T21、T22：**指定交錯順序**的競態測試（設計文件 10 第 1 節）。
///
/// 這些案例不能靠「同時送出然後看結果」——那樣綠燈可能只是運氣。
/// 這裡用 <see cref="BookingGatePause"/> 把一方停在「已握有買家 gate」的那一刻，
/// 讓另一方確實排在後面，再放行。同一組情境**兩個方向各跑一次**。
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class BookingInterleavingTests(SqlServerFixture fixture)
{
    // ── T15：付款與取消交錯 ───────────────────────────────────────────

    [Fact] // T15：付款先拿到 gate
    public async Task When_checkout_wins_the_gate_the_cancel_that_follows_is_rejected()
    {
        var pause = new BookingGatePause();
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString, GatePauseRegistration.Using(pause));

        var holdId = await CreateHoldAsync(factory, buyers[0]);

        var parked = pause.ArmAsync();
        using var payer = factory.ClientFor(buyers[0]);
        var checkout = payer.CheckoutAsync(holdId, "Succeeded", BookingTestHelpers.NewKey());
        await parked;                                  // 付款已握有買家 gate，停住

        using var canceller = factory.ClientFor(buyers[0]);
        var cancel = canceller.CancelAsync(holdId);    // 會在 SQL Server 裡排隊等 gate
        await Task.Delay(200);                          // 讓它確實排進去

        pause.Release();

        Assert.Equal(HttpStatusCode.Created, (await checkout).StatusCode);
        var cancelResponse = await cancel;
        Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);
        Assert.Equal("HoldNotActive", (await cancelResponse.ReadJsonAsync()).GetProperty("code").GetString());

        // 沒有「訂單存在但座位可售」這種狀態
        Assert.Equal(1, await db.CountAsync("Orders"));
        Assert.Equal("Sold", await db.SeatStatusAsync(Ids.ConcertSeat(1, 1)));
    }

    [Fact] // T15：取消先拿到 gate
    public async Task When_cancel_wins_the_gate_the_checkout_that_follows_is_rejected()
    {
        var pause = new BookingGatePause();
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString, GatePauseRegistration.Using(pause));

        var holdId = await CreateHoldAsync(factory, buyers[0]);

        var parked = pause.ArmAsync();
        using var canceller = factory.ClientFor(buyers[0]);
        var cancel = canceller.CancelAsync(holdId);
        await parked;

        using var payer = factory.ClientFor(buyers[0]);
        var checkout = payer.CheckoutAsync(holdId, "Succeeded", BookingTestHelpers.NewKey());
        await Task.Delay(200);

        pause.Release();

        Assert.Equal(HttpStatusCode.OK, (await cancel).StatusCode);
        var checkoutResponse = await checkout;
        Assert.Equal(HttpStatusCode.Conflict, checkoutResponse.StatusCode);
        Assert.Equal("HoldNotActive", (await checkoutResponse.ReadJsonAsync()).GetProperty("code").GetString());

        Assert.Equal(0, await db.CountAsync("Orders"));
        Assert.Equal("Available", await db.SeatStatusAsync(Ids.ConcertSeat(1, 1)));
    }

    // ── T16：累計上限的競態 ──────────────────────────────────────────

    [Fact] // T16
    public async Task A_hold_that_waits_for_the_gate_re_reads_the_paid_count_afterwards()
    {
        var pause = new BookingGatePause();
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString, GatePauseRegistration.Using(pause));

        // 先保留 2 張演唱會票（上限 4）
        var holdId = await CreateHoldAsync(factory, buyers[0], Ids.ConcertSeat(1, 1), Ids.ConcertSeat(1, 2));

        var parked = pause.ArmAsync();
        using var payer = factory.ClientFor(buyers[0]);
        var checkout = payer.CheckoutAsync(holdId, "Succeeded", BookingTestHelpers.NewKey());
        await parked;                              // 付款停在買家 gate 之內，還沒 commit

        // 這時候再開一筆 3 張。如果它讀到的是「已付款 0 張」，就會通過 → 總共 5 張
        using var second = factory.ClientFor(buyers[0]);
        var another = second.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA,
                Ids.ConcertSeat(2, 1), Ids.ConcertSeat(2, 2), Ids.ConcertSeat(2, 3)),
            BookingTestHelpers.NewKey());
        await Task.Delay(200);

        pause.Release();

        Assert.Equal(HttpStatusCode.Created, (await checkout).StatusCode);

        // 買家 gate 讓它排在後面，所以它重讀到的已付款是 2 → 2＋3 > 4
        var anotherResponse = await another;
        Assert.Equal(HttpStatusCode.Conflict, anotherResponse.StatusCode);
        Assert.Equal("LimitExceeded", (await anotherResponse.ReadJsonAsync()).GetProperty("code").GetString());

        // 不變量：已付款席數 ＋ 仍有效的保留席數 <= 4
        Assert.Equal(2, await db.CountAsync("OrderItems"));
        Assert.Equal(0, await db.CountAsync("SeatHolds", "Status = 'Active'"));
    }

    // ── T22：等 gate 的時候保留過期了 ─────────────────────────────────

    [Fact] // T22
    public async Task A_checkout_that_waits_past_the_expiry_is_judged_by_the_time_it_actually_runs()
    {
        var pause = new BookingGatePause();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);

        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString, services =>
        {
            GatePauseRegistration.Using(pause)(services);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);   // 要註冊成 TimeProvider，不是 FakeTimeProvider
        });

        var holdId = await CreateHoldAsync(factory, buyers[0]);

        var parked = pause.ArmAsync();
        using var payer = factory.ClientFor(buyers[0]);
        var checkout = payer.CheckoutAsync(holdId, "Succeeded", BookingTestHelpers.NewKey());
        await parked;

        // 它停在「拿到 gate、還沒讀時間」的位置——這時候把業務時鐘推過到期
        clock.Advance(TimeSpan.FromMinutes(6));
        pause.Release();

        var response = await checkout;

        // 時間代表「系統接受這次操作的時點」，不是「請求到達的時點」
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("HoldExpired", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(0, await db.CountAsync("Orders"));
    }

    // ── T17：同一個 key 用在不同的資源 ───────────────────────────────

    [Fact] // T17
    public async Task One_key_belongs_to_one_command_not_just_one_body()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.ClientFor(buyers[0]);

        var key = BookingTestHelpers.NewKey();

        var first = await client.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)), key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // 同一個 key、完全相同的 body，但**換一個場次** → 不是同一個命令
        var otherPerformance = await client.PostHoldAsync(2,
            new { sectionId = 21, quantity = 1, selectionMode = "Manual", seatIds = new[] { 2101 } }, key);
        Assert.Equal(HttpStatusCode.Conflict, otherPerformance.StatusCode);
        Assert.Equal("IdempotencyKeyReuse",
                     (await otherPerformance.ReadJsonAsync()).GetProperty("code").GetString());

        // 同一個 key 換成付款端點 → 也不是同一個命令
        var asCheckout = await client.CheckoutAsync(await first.HoldIdAsync(), "Succeeded", key);
        Assert.Equal(HttpStatusCode.Conflict, asCheckout.StatusCode);

        Assert.Equal(1, await db.CountAsync("IdempotencyRecords"));
    }

    [Fact] // T17：座位順序不同視為同一個請求
    public async Task The_same_seats_listed_in_a_different_order_replay_instead_of_conflicting()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.ClientFor(buyers[0]);

        var key = BookingTestHelpers.NewKey();
        int a = Ids.ConcertSeat(1, 1), b = Ids.ConcertSeat(1, 2);

        var first = await client.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, a, b), key);
        var reordered = await client.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, b, a), key);

        Assert.Equal(HttpStatusCode.Created, reordered.StatusCode);
        Assert.Equal(await first.HoldIdAsync(), await reordered.HoldIdAsync());
        Assert.Equal(1, await db.CountAsync("SeatHolds"));
    }

    // ── T21：commit 成功但回覆遺失／commit 之前失敗 ──────────────────

    [Fact] // T21
    public async Task If_the_commit_succeeded_but_the_answer_was_lost_the_same_key_returns_that_order()
    {
        var interceptor = new CommitFailureInterceptor();
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString, interceptor: interceptor);
        using var client = factory.ClientFor(buyers[0]);

        var holdId = await CreateHoldAsync(factory, buyers[0]);
        var payKey = BookingTestHelpers.NewKey();

        interceptor.FailOnceAfterCommit();
        var lost = await client.CheckoutAsync(holdId, "Succeeded", payKey);

        // 客戶端看到的是失敗——但資料庫其實已經 commit 了
        Assert.Equal(HttpStatusCode.InternalServerError, lost.StatusCode);
        Assert.Equal(1, await db.CountAsync("Orders"));

        // 唯一正確的補救：用同一個 key 重送。必須拿到**原本那一張**訂單
        var retry = await client.CheckoutAsync(holdId, "Succeeded", payKey);

        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(1, await db.CountAsync("Orders"));

        var orderId = await db.ScalarAsync<Guid>("SELECT TOP 1 Id FROM dbo.Orders;");
        Assert.Equal(orderId, (await retry.ReadJsonAsync()).GetProperty("id").GetGuid());
    }

    [Fact] // T21：commit 之前失敗
    public async Task If_it_failed_before_the_commit_nothing_is_left_behind_and_nothing_is_faked()
    {
        var interceptor = new CommitFailureInterceptor();
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString, interceptor: interceptor);
        using var client = factory.ClientFor(buyers[0]);

        var holdId = await CreateHoldAsync(factory, buyers[0]);

        var payKey = BookingTestHelpers.NewKey();
        interceptor.FailOnceBeforeCommit();
        var failed = await client.CheckoutAsync(holdId, "Succeeded", payKey);

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);

        // 整段 rollback：沒有訂單、這個 key 沒有冪等紀錄、座位還是 Held、保留還是 Active
        // （建立保留那一次的紀錄仍在，所以要指名這個 key，不能數總筆數）
        Assert.Equal(0, await db.CountAsync("Orders"));
        Assert.Equal(0, await db.CountAsync("IdempotencyRecords", "[Key] = @key", ("@key", payKey)));
        Assert.Equal("Held", await db.SeatStatusAsync(Ids.ConcertSeat(1, 1)));
        Assert.Equal("Active", await db.ScalarAsync<string>(
            "SELECT Status FROM dbo.SeatHolds WHERE Id = @id;", ("@id", holdId)));

        // 而且它還能正常付款——失敗沒有讓保留變成殭屍
        var retried = await client.CheckoutAsync(holdId, "Succeeded", BookingTestHelpers.NewKey());
        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
    }

    private static async Task<Guid> CreateHoldAsync(TicketingApiFactory factory, Guid buyer,
                                                    params int[] seatIds)
    {
        if (seatIds.Length == 0) seatIds = [Ids.ConcertSeat(1, 1)];

        using var client = factory.ClientFor(buyer);
        var response = await client.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, seatIds), BookingTestHelpers.NewKey());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.HoldIdAsync();
    }
}
