using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Ticketing.Api.Tests.Infrastructure;

namespace Ticketing.Api.Tests;

/// <summary>
/// T01～T10、T14：**真的 SQL Server、真的併發**（設計文件 10 第 2.2 節）。
///
/// 這一組測的不是「有沒有丟例外」，而是**資料庫最終狀態對不對**：
/// 幾個 Held、幾張訂單、哪一席歸誰。HTTP 回應只是線索，資料才是證據。
///
/// ⚠️ 開發機是 arm64，SQL Server 容器是模擬執行；權威結果以 CI 的 x86-64 runner 為準。
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class BookingConcurrencyTests(SqlServerFixture fixture)
{
    [Fact] // T01
    public async Task Fifty_buyers_grabbing_one_seat_produce_exactly_one_hold()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(50);
        using var factory = new TicketingApiFactory(db.ConnectionString);

        var seat = Ids.ConcertSeat(1, 1);

        var responses = await Task.WhenAll(buyers.Select(async buyer =>
        {
            using var client = factory.ClientFor(buyer);
            return await client.PostHoldAsync(Ids.ConcertPerformance,
                BookingTestHelpers.Manual(Ids.ConcertSectionA, seat), BookingTestHelpers.NewKey());
        }));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(49, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        foreach (var conflict in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.Equal("SeatUnavailable", (await conflict.ReadJsonAsync()).GetProperty("code").GetString());

        // 資料才是證據：那一席只被一筆保留拿走
        Assert.Equal("Held", await db.SeatStatusAsync(seat));
        Assert.Equal(1, await db.CountAsync("SeatHolds", "PerformanceId = 1 AND Status = 'Active'"));
        Assert.Equal(1, await db.CountAsync("SeatHoldItems", "SeatId = @seat", ("@seat", seat)));
    }

    [Fact] // T02
    public async Task Picking_three_seats_where_one_is_gone_fails_as_a_batch()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(2);
        using var factory = new TicketingApiFactory(db.ConnectionString);

        int a = Ids.ConcertSeat(1, 1), b = Ids.ConcertSeat(1, 2), taken = Ids.ConcertSeat(1, 3);
        await db.BlockSeatsAsync(Ids.ConcertPerformance, buyers[1], TimeSpan.FromMinutes(30), taken);

        using var client = factory.ClientFor(buyers[0]);
        var response = await client.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, a, b, taken), BookingTestHelpers.NewKey());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // 「整批成立或整批失敗」：另外兩席不能被半途佔走
        Assert.Equal("Available", await db.SeatStatusAsync(a));
        Assert.Equal("Available", await db.SeatStatusAsync(b));
    }

    [Fact] // T03
    public async Task One_person_in_two_tabs_only_gets_one_hold()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);

        async Task<HttpResponseMessage> Hold(params int[] seatIds)
        {
            using var client = factory.ClientFor(buyers[0]);
            return await client.PostHoldAsync(Ids.ConcertPerformance,
                BookingTestHelpers.Manual(Ids.ConcertSectionA, seatIds), BookingTestHelpers.NewKey());
        }

        var results = await Task.WhenAll(
            Hold(Ids.ConcertSeat(1, 1), Ids.ConcertSeat(1, 2), Ids.ConcertSeat(1, 3)),
            Hold(Ids.ConcertSeat(2, 1), Ids.ConcertSeat(2, 2), Ids.ConcertSeat(2, 3)));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));

        var rejected = results.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        var problem = await rejected.ReadJsonAsync();
        Assert.Equal("ActiveHoldExists", problem.GetProperty("code").GetString());

        // 錯誤裡要附上既有的 holdId，前端才知道該把人導去哪一筆
        Assert.True(problem.TryGetProperty("holdId", out var holdId));
        Assert.NotEqual(Guid.Empty, holdId.GetGuid());

        Assert.Equal(1, await db.CountAsync("SeatHolds", "Status = 'Active'"));
    }

    [Fact] // T04
    public async Task Paid_seats_count_towards_the_limit_and_the_limit_depends_on_the_category()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(2);
        using var factory = new TicketingApiFactory(db.ConnectionString);

        // 演唱會：先付 2 張，再要 3 張 → 2＋3 > 4
        using (var concertClient = factory.ClientFor(buyers[0]))
        {
            var hold = await concertClient.PostHoldAsync(Ids.ConcertPerformance,
                BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1), Ids.ConcertSeat(1, 2)),
                BookingTestHelpers.NewKey());
            await concertClient.CheckoutAsync(await hold.HoldIdAsync(), "Succeeded", BookingTestHelpers.NewKey());

            var third = await concertClient.PostHoldAsync(Ids.ConcertPerformance,
                BookingTestHelpers.Manual(Ids.ConcertSectionA,
                    Ids.ConcertSeat(2, 1), Ids.ConcertSeat(2, 2), Ids.ConcertSeat(2, 3)),
                BookingTestHelpers.NewKey());

            Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
            Assert.Equal("LimitExceeded", (await third.ReadJsonAsync()).GetProperty("code").GetString());
        }

        // 運動賽事：同樣的數字，上限 6 → 2＋3 = 5，可以
        using var sportClient = factory.ClientFor(buyers[1]);
        var sportHold = await sportClient.PostHoldAsync(Ids.SportPerformance,
            BookingTestHelpers.Manual(Ids.SportSectionA, Ids.SportSeat(1, 1), Ids.SportSeat(1, 2)),
            BookingTestHelpers.NewKey());
        await sportClient.CheckoutAsync(await sportHold.HoldIdAsync(), "Succeeded", BookingTestHelpers.NewKey());

        var sportThird = await sportClient.PostHoldAsync(Ids.SportPerformance,
            BookingTestHelpers.Manual(Ids.SportSectionA,
                Ids.SportSeat(2, 1), Ids.SportSeat(2, 2), Ids.SportSeat(2, 3)),
            BookingTestHelpers.NewKey());

        Assert.Equal(HttpStatusCode.Created, sportThird.StatusCode);
    }

    [Fact] // T05
    public async Task The_same_key_sent_five_times_in_parallel_creates_one_hold()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);

        var key = BookingTestHelpers.NewKey();
        var body = BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1), Ids.ConcertSeat(1, 2));

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            using var client = factory.ClientFor(buyers[0]);
            return await client.PostHoldAsync(Ids.ConcertPerformance, body, key);
        }));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var ids = new List<Guid>();
        foreach (var response in responses) ids.Add(await response.HoldIdAsync());
        Assert.Single(ids.Distinct());                       // 五次拿到同一筆

        Assert.Equal(1, await db.CountAsync("SeatHolds"));
        Assert.Equal(1, await db.CountAsync("IdempotencyRecords"));

        // 同一個 key 換座位 → 409，不是「大概是想重送吧」
        using var client2 = factory.ClientFor(buyers[0]);
        var reused = await client2.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(3, 1)), key);

        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal("IdempotencyKeyReuse", (await reused.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact] // T06
    public async Task Paying_again_with_the_same_key_replays_and_a_new_key_returns_the_existing_order()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.ClientFor(buyers[0]);

        var hold = await client.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)), BookingTestHelpers.NewKey());
        var holdId = await hold.HoldIdAsync();

        var payKey = BookingTestHelpers.NewKey();
        var first = await client.CheckoutAsync(holdId, "Succeeded", payKey);
        var replay = await client.CheckoutAsync(holdId, "Succeeded", payKey);
        var withNewKey = await client.CheckoutAsync(holdId, "Succeeded", BookingTestHelpers.NewKey());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);          // 原 key 重播原狀態
        Assert.Equal(HttpStatusCode.OK, withNewKey.StatusCode);           // 新 key：已經有訂單了

        var firstId = (await first.ReadJsonAsync()).GetProperty("id").GetGuid();
        Assert.Equal(firstId, (await replay.ReadJsonAsync()).GetProperty("id").GetGuid());
        Assert.Equal(firstId, (await withNewKey.ReadJsonAsync()).GetProperty("id").GetGuid());

        Assert.Equal(1, await db.CountAsync("Orders"));
        Assert.Equal("Sold", await db.SeatStatusAsync(Ids.ConcertSeat(1, 1)));
    }

    [Fact] // T07
    public async Task A_mock_failure_can_be_retried_and_the_expiry_never_moves()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.ClientFor(buyers[0]);

        var hold = await client.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)), BookingTestHelpers.NewKey());

        // 回應的內容只讀得到一次（HttpContent 是串流），所以讀一次後兩個值都從它取
        var created = await hold.ReadJsonAsync();
        var holdId = created.GetProperty("id").GetGuid();
        var expiresAt = created.GetProperty("expiresAtUtc").GetDateTimeOffset();

        var failKey = BookingTestHelpers.NewKey();
        var failed = await client.CheckoutAsync(holdId, "Failed", failKey);
        var failedAgain = await client.CheckoutAsync(holdId, "Failed", failKey);
        var succeeded = await client.CheckoutAsync(holdId, "Succeeded", BookingTestHelpers.NewKey());

        Assert.Equal(HttpStatusCode.PaymentRequired, failed.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, failedAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Created, succeeded.StatusCode);

        // 402 的 body 是重新產生的 ProblemDetails，帶**當次**的 traceId
        var problem = await failedAgain.ReadJsonAsync();
        Assert.Equal("MockPaymentFailed", problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));

        // 失敗不會延長保留：不然一直按失敗就能無限佔位
        var current = await (await client.GetAsync($"/api/v1/holds/{holdId}")).ReadJsonAsync();
        Assert.Equal(expiresAt, current.GetProperty("expiresAtUtc").GetDateTimeOffset());
    }

    [Fact] // T08
    public async Task At_the_exact_expiry_instant_payment_fails_and_the_seat_is_free_again()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(2);

        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = WithClock(db.ConnectionString, clock);

        int seat = Ids.ConcertSeat(1, 1);

        using var first = factory.ClientFor(buyers[0]);
        var hold = await first.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, seat), BookingTestHelpers.NewKey());
        var holdId = await hold.HoldIdAsync();

        clock.Advance(TimeSpan.FromMinutes(5));       // 剛好到期那一刻

        var tooLate = await first.CheckoutAsync(holdId, "Succeeded", BookingTestHelpers.NewKey());

        Assert.Equal(HttpStatusCode.Conflict, tooLate.StatusCode);
        Assert.Equal("HoldExpired", (await tooLate.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(0, await db.CountAsync("Orders"));

        // 過期的 Held 對別人來說就是可用——不需要背景清理程式
        using var second = factory.ClientFor(buyers[1]);
        var taken = await second.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, seat), BookingTestHelpers.NewKey());

        Assert.Equal(HttpStatusCode.Created, taken.StatusCode);
    }

    [Fact] // T09
    public async Task Cancelling_an_expired_hold_does_not_steal_the_seat_back_from_its_new_owner()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(2);

        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = WithClock(db.ConnectionString, clock);

        int seat = Ids.ConcertSeat(1, 1);

        using var alice = factory.ClientFor(buyers[0]);
        var aliceHold = await (await alice.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, seat), BookingTestHelpers.NewKey())).HoldIdAsync();

        clock.Advance(TimeSpan.FromMinutes(6));

        using var bob = factory.ClientFor(buyers[1]);
        var bobHold = await (await bob.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, seat), BookingTestHelpers.NewKey())).HoldIdAsync();

        // Alice 現在才按「取消」。條件更新的 WHERE 有 HoldId = 她的，所以碰不到 Bob 的座位
        var cancelled = await alice.CancelAsync(aliceHold);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        Assert.Equal("Held", await db.SeatStatusAsync(seat));
        Assert.Equal(bobHold, await db.ScalarAsync<Guid>(
            "SELECT HoldId FROM dbo.Seats WHERE Id = @id;", ("@id", seat)));
    }

    [Fact] // T10
    public async Task Contiguous_allocation_moves_to_the_next_row_and_never_scatters()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(3);
        using var factory = new TicketingApiFactory(db.ConnectionString);

        // 第 1 排 16 席，卡住第 6 與第 12 → 只剩 5、5、4 的三段，容不下 6 席
        await db.BlockSeatsAsync(Ids.SportPerformance, buyers[1], TimeSpan.FromMinutes(30),
                                 Ids.SportSeat(1, 6), Ids.SportSeat(1, 12));

        using var client = factory.ClientFor(buyers[0]);
        var response = await client.PostHoldAsync(Ids.SportPerformance,
            BookingTestHelpers.Contiguous(Ids.SportSectionA, 6), BookingTestHelpers.NewKey());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var seats = (await response.ReadJsonAsync()).GetProperty("items")
            .EnumerateArray().Select(i => i.GetProperty("seatId").GetInt32()).ToArray();

        // 整段都在第 2 排，而且號碼連續——不跨排、不拆單
        Assert.Equal(Enumerable.Range(Ids.SportSeat(2, 1), 6), seats);

        // 每一排都卡成最多 5 連 → 找不到 6 連，回 NoContiguousSeats 而不是硬湊
        for (var row = 2; row <= 5; row++)
            await db.BlockSeatsAsync(Ids.SportPerformance, (await db.CreateBuyersAsync(1))[0],
                                     TimeSpan.FromMinutes(30),
                                     Ids.SportSeat(row, 6), Ids.SportSeat(row, 12));

        using var second = factory.ClientFor(buyers[2]);
        var impossible = await second.PostHoldAsync(Ids.SportPerformance,
            BookingTestHelpers.Contiguous(Ids.SportSectionA, 6), BookingTestHelpers.NewKey());

        Assert.Equal(HttpStatusCode.Conflict, impossible.StatusCode);
        Assert.Equal("NoContiguousSeats",
                     (await impossible.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact] // T14
    public async Task After_my_own_hold_expires_I_can_hold_again_in_the_same_performance()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);

        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var factory = WithClock(db.ConnectionString, clock);
        using var client = factory.ClientFor(buyers[0]);

        var firstHold = await (await client.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)),
            BookingTestHelpers.NewKey())).HoldIdAsync();

        clock.Advance(TimeSpan.FromMinutes(6));

        // 舊的那筆在資料庫還是 Active，會擋住 filtered unique index——
        // 所以新保留必須先把它持久化成 Expired
        var second = await client.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(2, 1)),
            BookingTestHelpers.NewKey());

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        Assert.Equal("Expired", await db.ScalarAsync<string>(
            "SELECT Status FROM dbo.SeatHolds WHERE Id = @id;", ("@id", firstHold)));
        Assert.Equal(1, await db.CountAsync("SeatHolds", "Status = 'Active'"));
        Assert.Equal("Available", await db.SeatStatusAsync(Ids.ConcertSeat(1, 1)));   // 舊座位放掉了
    }

    private static TicketingApiFactory WithClock(string connectionString, TimeProvider clock)
        => new(connectionString, services =>
        {
            // 只換業務時鐘。JWT 的簽發／驗證仍用真實時間（設計文件 05 第 4.2 節），
            // 所以撥動這個時鐘不會讓 token 突然失效
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(clock);
        });
}
