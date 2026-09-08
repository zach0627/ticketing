using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Ticketing.Api.Tests.Infrastructure;

namespace Ticketing.Api.Tests;

/// <summary>
/// T11、T12（admin 部分）、T18、T23：管理後台與重置（設計文件 14 第 8 節）。
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class AdminEndpointTests(SqlServerFixture fixture)
{
    // ── T12：授權 ────────────────────────────────────────────────────

    [Fact] // T12
    public async Task Admin_endpoints_are_401_without_a_token_and_403_for_a_customer()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);

        using var anonymous = factory.CreateClient();
        using var customer = factory.ClientFor(buyers[0]);          // role = Customer

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/admin/dashboard")).StatusCode);

        foreach (var response in await Task.WhenAll(
            customer.GetAsync("/api/v1/admin/dashboard"),
            customer.GetAsync("/api/v1/admin/orders"),
            customer.SendJsonAsync(HttpMethod.Patch, "/api/v1/admin/performances/1",
                                   new { isSalesPaused = true }, BookingTestHelpers.NewKey()),
            customer.SendJsonAsync(HttpMethod.Post, "/api/v1/admin/reset",
                                   new { confirmation = "RESET" }, BookingTestHelpers.NewKey())))
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("Forbidden", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        }

        // 沒有任何東西被改到
        Assert.Equal(0, await db.CountAsync("AdminAudits"));
    }

    // ── 儀表板與訂單 ─────────────────────────────────────────────────

    [Fact]
    public async Task The_dashboard_counts_only_holds_that_have_not_expired_yet()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(2);
        using var factory = new TicketingApiFactory(db.ConnectionString);

        // 一筆還有效、一筆資料庫是 Active 但早就過期
        await db.BlockSeatsAsync(Ids.ConcertPerformance, buyers[0], TimeSpan.FromMinutes(30),
                                 Ids.ConcertSeat(1, 1));
        await db.BlockSeatsAsync(Ids.ConcertPerformance, buyers[1], TimeSpan.FromMinutes(-30),
                                 Ids.ConcertSeat(1, 2));

        using var admin = AdminClient(factory);
        var dashboard = await (await admin.GetAsync("/api/v1/admin/dashboard")).ReadJsonAsync();

        Assert.Equal(2, await db.CountAsync("SeatHolds", "Status = 'Active'"));   // 資料庫是 2
        Assert.Equal(1, dashboard.GetProperty("activeHolds").GetInt32());          // 對外只算沒過期的
        Assert.Equal(15, dashboard.GetProperty("performances").GetArrayLength());
        Assert.Equal("OnSale", dashboard.GetProperty("performances")[0]
                                        .GetProperty("salesStatus").GetString());
    }

    [Fact]
    public async Task Admin_orders_show_the_buyer_display_name_but_never_the_email()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);

        using var buyer = factory.ClientFor(buyers[0]);
        var hold = await buyer.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)), BookingTestHelpers.NewKey());
        await buyer.CheckoutAsync(await hold.HoldIdAsync(), "Succeeded", BookingTestHelpers.NewKey());

        using var admin = AdminClient(factory);
        var orders = await (await admin.GetAsync("/api/v1/admin/orders")).ReadJsonAsync();

        Assert.Equal(1, orders.GetProperty("total").GetInt32());
        var row = orders.GetProperty("items")[0];
        Assert.Equal("買家 0", row.GetProperty("buyerDisplayName").GetString());
        Assert.Equal(Ids.ConcertPerformance, row.GetProperty("performanceId").GetInt32());
        Assert.DoesNotContain("email", row.EnumerateObject().Select(p => p.Name),
                              StringComparer.OrdinalIgnoreCase);

        // 依場次篩選
        var filtered = await (await admin.GetAsync("/api/v1/admin/orders?performanceId=99")).ReadJsonAsync();
        Assert.Equal(0, filtered.GetProperty("total").GetInt32());
    }

    // ── T11：重置 ────────────────────────────────────────────────────

    [Fact] // T11
    public async Task A_reset_clears_the_purchases_frees_every_seat_and_puts_the_shows_back_on_sale()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var admin = AdminClient(factory);

        // 先製造出「需要重置」的狀態：有訂單、有保留、場次被暫停、日期已經過去
        using (var buyer = factory.ClientFor(buyers[0]))
        {
            var hold = await buyer.PostHoldAsync(Ids.ConcertPerformance,
                BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1), Ids.ConcertSeat(1, 2)),
                BookingTestHelpers.NewKey());
            await buyer.CheckoutAsync(await hold.HoldIdAsync(), "Succeeded", BookingTestHelpers.NewKey());
        }
        await db.BlockSeatsAsync(Ids.SportPerformance, buyers[0], TimeSpan.FromMinutes(30),
                                 Ids.SportSeat(1, 1));
        await admin.SendJsonAsync(HttpMethod.Patch, $"/api/v1/admin/performances/{Ids.ConcertPerformance}",
                                  new { isSalesPaused = true }, BookingTestHelpers.NewKey());
        await MakeDatesStaleAsync(db);

        var usersBefore = await db.CountAsync("AppUsers");
        var eventsBefore = await db.CountAsync("Events");

        var response = await admin.SendJsonAsync(HttpMethod.Post, "/api/v1/admin/reset",
                                                 new { confirmation = "RESET" }, BookingTestHelpers.NewKey());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadJsonAsync();
        Assert.Equal(1, result.GetProperty("ordersDeleted").GetInt32());
        Assert.Equal(3, result.GetProperty("seatsReleased").GetInt32());

        // 購買資料清光
        Assert.Equal(0, await db.CountAsync("Orders"));
        Assert.Equal(0, await db.CountAsync("OrderItems"));
        Assert.Equal(0, await db.CountAsync("SeatHolds"));
        Assert.Equal(0, await db.CountAsync("IdempotencyRecords"));

        // 座位全部可售
        Assert.Equal(2880, await db.CountAsync("Seats", "Status = 'Available'"));
        Assert.Equal(0, await db.CountAsync("Seats", "HoldId IS NOT NULL OR HeldUntilUtc IS NOT NULL"));

        // 帳號、活動、稽核**不動**
        Assert.Equal(usersBefore, await db.CountAsync("AppUsers"));
        Assert.Equal(eventsBefore, await db.CountAsync("Events"));
        Assert.Equal(2, await db.CountAsync("AdminAudits"));      // 暫停一筆 ＋ 重置一筆

        // 所有場次恢復可售，而且最早的一場在未來
        Assert.Equal(0, await db.CountAsync("Performances", "IsSalesPaused = 1"));
        var events = await (await admin.GetAsync("/api/v1/events?pageSize=30")).ReadJsonAsync();
        Assert.All(events.GetProperty("items").EnumerateArray(),
                   e => Assert.Equal("OnSale", e.GetProperty("salesStatus").GetString()));

        // 重置後買得到票——這才是重置的目的
        using var afterReset = factory.ClientFor(buyers[0]);
        var again = await afterReset.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)), BookingTestHelpers.NewKey());
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact] // T11：中途失敗
    public async Task A_reset_that_fails_before_commit_leaves_everything_exactly_as_it_was()
    {
        var interceptor = new CommitFailureInterceptor();
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString, interceptor: interceptor);

        using (var buyer = factory.ClientFor(buyers[0]))
        {
            var hold = await buyer.PostHoldAsync(Ids.ConcertPerformance,
                BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)),
                BookingTestHelpers.NewKey());
            await buyer.CheckoutAsync(await hold.HoldIdAsync(), "Succeeded", BookingTestHelpers.NewKey());
        }

        using var admin = AdminClient(factory);
        interceptor.FailOnceBeforeCommit();
        var failed = await admin.SendJsonAsync(HttpMethod.Post, "/api/v1/admin/reset",
                                               new { confirmation = "RESET" }, BookingTestHelpers.NewKey());

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);

        // 訂單、座位、稽核全部維持原狀
        Assert.Equal(1, await db.CountAsync("Orders"));
        Assert.Equal("Sold", await db.SeatStatusAsync(Ids.ConcertSeat(1, 1)));
        Assert.Equal(0, await db.CountAsync("AdminAudits"));
    }

    [Theory]
    [InlineData("reset")]
    [InlineData("RESET ")]
    [InlineData("")]
    public async Task A_reset_without_the_exact_confirmation_word_is_a_400(string confirmation)
    {
        await using var db = await fixture.SeededDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var admin = AdminClient(factory);

        var response = await admin.SendJsonAsync(HttpMethod.Post, "/api/v1/admin/reset",
                                                 new { confirmation }, BookingTestHelpers.NewKey());

        // 前端的確認框防不了直接打 API 的人，所以由模型驗證精確比對
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await db.CountAsync("AdminAudits"));
    }

    // ── T18：重置的回覆遺失 ──────────────────────────────────────────

    [Fact] // T18
    public async Task A_replayed_reset_returns_the_first_result_and_keeps_the_new_orders()
    {
        var interceptor = new CommitFailureInterceptor();
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString, interceptor: interceptor);
        using var admin = AdminClient(factory);

        using (var buyer = factory.ClientFor(buyers[0]))
        {
            var hold = await buyer.PostHoldAsync(Ids.ConcertPerformance,
                BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)),
                BookingTestHelpers.NewKey());
            await buyer.CheckoutAsync(await hold.HoldIdAsync(), "Succeeded", BookingTestHelpers.NewKey());
        }

        var resetKey = BookingTestHelpers.NewKey();

        // 重置真的做完並 commit 了，但回覆在半路上不見了
        interceptor.FailOnceAfterCommit();
        var lost = await admin.SendJsonAsync(HttpMethod.Post, "/api/v1/admin/reset",
                                             new { confirmation = "RESET" }, resetKey);
        Assert.Equal(HttpStatusCode.InternalServerError, lost.StatusCode);
        Assert.Equal(0, await db.CountAsync("Orders"));           // 其實已經清掉了

        // 新一輪：又有人買票了
        using (var buyer = factory.ClientFor(buyers[0]))
        {
            var hold = await buyer.PostHoldAsync(Ids.ConcertPerformance,
                BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(2, 1)),
                BookingTestHelpers.NewKey());
            await buyer.CheckoutAsync(await hold.HoldIdAsync(), "Succeeded", BookingTestHelpers.NewKey());
        }
        Assert.Equal(1, await db.CountAsync("Orders"));

        // 管理者用**同一個 key** 重送。這是整個設計最關鍵的一刻：
        // 如果去重紀錄跟訂單一起被刪掉，這次就會再清一次，把新訂單也刪了
        var replay = await admin.SendJsonAsync(HttpMethod.Post, "/api/v1/admin/reset",
                                               new { confirmation = "RESET" }, resetKey);

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(1, (await replay.ReadJsonAsync()).GetProperty("ordersDeleted").GetInt32());  // 第一次的數字
        Assert.Equal(1, await db.CountAsync("Orders"));           // ⭐ 新訂單活著
        Assert.Equal(1, await db.CountAsync("AdminAudits"));      // 只有第一次那一筆
    }

    [Fact] // T18：同 key 不同 actor
    public async Task Reusing_an_operation_id_for_a_different_admin_is_a_conflict()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);

        var key = BookingTestHelpers.NewKey();

        using var first = AdminClient(factory);
        Assert.Equal(HttpStatusCode.OK, (await first.SendJsonAsync(HttpMethod.Post, "/api/v1/admin/reset",
            new { confirmation = "RESET" }, key)).StatusCode);

        using var second = AdminClient(factory);       // 另一個管理者（不同 sub）
        var reused = await second.SendJsonAsync(HttpMethod.Post, "/api/v1/admin/reset",
                                                new { confirmation = "RESET" }, key);

        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal("IdempotencyKeyReuse", (await reused.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(1, await db.CountAsync("AdminAudits"));
    }

    // ── T23：暫停與新保留的先後 ──────────────────────────────────────

    [Fact] // T23：暫停先完成
    public async Task When_the_pause_commits_first_the_next_hold_is_rejected()
    {
        var pause = new BookingGatePause();
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString, GatePauseRegistration.Using(pause));

        var parked = pause.ArmAsync(GateStage.PerformanceWrite);

        using var admin = AdminClient(factory);
        var pausing = admin.SendJsonAsync(HttpMethod.Patch,
            $"/api/v1/admin/performances/{Ids.ConcertPerformance}",
            new { isSalesPaused = true }, BookingTestHelpers.NewKey());
        await parked;                                   // 管理操作握著場次的 XLOCK

        using var buyer = factory.ClientFor(buyers[0]);
        var holding = buyer.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)), BookingTestHelpers.NewKey());
        await Task.Delay(200);

        pause.Release();

        Assert.Equal(HttpStatusCode.OK, (await pausing).StatusCode);
        var holdResponse = await holding;
        Assert.Equal(HttpStatusCode.Conflict, holdResponse.StatusCode);
        Assert.Equal("NotOnSale", (await holdResponse.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(0, await db.CountAsync("SeatHolds"));
    }

    [Fact] // T23：保留先完成
    public async Task When_the_hold_commits_first_it_survives_the_pause_and_can_still_be_paid()
    {
        var pause = new BookingGatePause();
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString, GatePauseRegistration.Using(pause));

        var parked = pause.ArmAsync(GateStage.Buyer);

        using var buyer = factory.ClientFor(buyers[0]);
        var holding = buyer.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)), BookingTestHelpers.NewKey());
        await parked;

        using var admin = AdminClient(factory);
        var pausing = admin.SendJsonAsync(HttpMethod.Patch,
            $"/api/v1/admin/performances/{Ids.ConcertPerformance}",
            new { isSalesPaused = true }, BookingTestHelpers.NewKey());
        await Task.Delay(200);

        pause.Release();

        var holdResponse = await holding;
        Assert.Equal(HttpStatusCode.Created, holdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await pausing).StatusCode);

        // 暫停只擋**新的**保留。已經拿到的人有權完成他的購買
        var paid = await buyer.CheckoutAsync(await holdResponse.HoldIdAsync(), "Succeeded",
                                             BookingTestHelpers.NewKey());
        Assert.Equal(HttpStatusCode.Created, paid.StatusCode);
        Assert.Equal(1, await db.CountAsync("Orders"));
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static HttpClient AdminClient(TicketingApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.Create(subject: Guid.NewGuid(), role: "Admin"));
        return client;
    }

    /// <summary>把所有場次往前搬到過去，模擬「seed 資料放太久」的展示情境。</summary>
    private static async Task MakeDatesStaleAsync(TestDatabase db)
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Performances
            SET StartsAtUtc      = DATEADD(day, -40, StartsAtUtc),
                SalesOpensAtUtc  = DATEADD(day, -40, SalesOpensAtUtc),
                SalesClosesAtUtc = DATEADD(day, -40, SalesClosesAtUtc);
            """;
        await command.ExecuteNonQueryAsync();
    }
}
