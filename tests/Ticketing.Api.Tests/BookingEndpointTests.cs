using System.Net;
using System.Text.Json;
using Ticketing.Api.Tests.Infrastructure;

namespace Ticketing.Api.Tests;

/// <summary>
/// 購票與訂單端點的**契約**：狀態碼、欄位、授權、錯誤格式（設計文件 13）。
/// T12 的「本人資源」部分也在這裡：別人的保留與訂單一律 404。
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class BookingEndpointTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Every_booking_endpoint_requires_a_login()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var anonymous = factory.CreateClient();

        var id = Guid.NewGuid();

        foreach (var response in await Task.WhenAll(
            anonymous.PostHoldAsync(Ids.ConcertPerformance,
                BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)),
                BookingTestHelpers.NewKey()),
            anonymous.GetAsync($"/api/v1/holds/{id}"),
            anonymous.GetAsync($"/api/v1/me/holds?performanceId={Ids.ConcertPerformance}"),
            anonymous.CancelAsync(id),
            anonymous.GetAsync("/api/v1/orders"),
            anonymous.GetAsync($"/api/v1/orders/{id}")))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact] // T12 的本人資源部分
    public async Task Other_peoples_holds_and_orders_are_not_found_rather_than_forbidden()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(2);
        using var factory = new TicketingApiFactory(db.ConnectionString);

        using var owner = factory.ClientFor(buyers[0]);
        var hold = await owner.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)), BookingTestHelpers.NewKey());
        var holdId = await hold.HoldIdAsync();

        var order = await owner.CheckoutAsync(holdId, "Succeeded", BookingTestHelpers.NewKey());
        var orderId = (await order.ReadJsonAsync()).GetProperty("id").GetGuid();

        using var stranger = factory.ClientFor(buyers[1]);

        // 403 等於承認「這個東西存在」。一律 404
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/holds/{holdId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.CancelAsync(holdId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/orders/{orderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.CheckoutAsync(holdId, "Succeeded", BookingTestHelpers.NewKey())).StatusCode);
    }

    [Fact]
    public async Task A_missing_or_malformed_idempotency_key_is_a_400()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.ClientFor(buyers[0]);

        var body = BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1));

        // 沒有標頭
        var missing = await client.PostHoldAsync(Ids.ConcertPerformance, body);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        // 不是 UUID
        var malformed = await client.PostHoldAsync(Ids.ConcertPerformance, body, "not-a-uuid");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("ValidationFailed", (await malformed.ReadJsonAsync()).GetProperty("code").GetString());

        // 送了兩個同名標頭（ASP.NET 會併成 "a,b"）——一樣不合法
        using var duplicate = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/performances/{Ids.ConcertPerformance}/holds")
        {
            Content = System.Net.Http.Json.JsonContent.Create(body, options: BookingTestHelpers.Json)
        };
        duplicate.Headers.Add("Idempotency-Key", new[] { BookingTestHelpers.NewKey(), BookingTestHelpers.NewKey() });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(duplicate)).StatusCode);

        Assert.Equal(0, await db.CountAsync("SeatHolds"));
    }

    [Theory]
    [InlineData("""{"sectionId":11,"quantity":2,"selectionMode":"Manual","seatIds":[1101]}""")]       // 數量不符
    [InlineData("""{"sectionId":11,"quantity":2,"selectionMode":"Manual","seatIds":[1101,1101]}""")]  // 重複
    [InlineData("""{"sectionId":11,"quantity":0,"selectionMode":"Manual","seatIds":[]}""")]           // 0 張
    [InlineData("""{"sectionId":11,"quantity":9,"selectionMode":"Manual","seatIds":[1101]}""")]       // 超過絕對上限
    [InlineData("""{"sectionId":11,"quantity":1,"selectionMode":"Everything","seatIds":[]}""")]       // 未知模式
    [InlineData("""{"sectionId":11,"quantity":1,"selectionMode":1,"seatIds":[]}""")]                  // 數字 enum
    public async Task Bad_hold_requests_are_rejected_with_a_validation_error(string json)
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.ClientFor(buyers[0]);

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/performances/{Ids.ConcertPerformance}/holds")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Idempotency-Key", BookingTestHelpers.NewKey());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ValidationFailed", (await response.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(0, await db.CountAsync("SeatHolds"));
    }

    [Fact]
    public async Task The_hold_written_by_post_and_the_hold_read_by_get_have_the_same_shape()
    {
        // 冪等紀錄存的是 Application 自己序列化的 JSON，而 GET 走的是 MVC 的序列化設定。
        // 兩邊的設定漂移的話，重播回來的東西會跟正常回應長得不一樣——這個測試就是在釘住它
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.ClientFor(buyers[0]);

        var created = await (await client.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)),
            BookingTestHelpers.NewKey())).ReadJsonAsync();

        var fetched = await (await client.GetAsync($"/api/v1/holds/{created.GetProperty("id").GetGuid()}"))
            .ReadJsonAsync();

        Assert.Equal(FieldNames(created), FieldNames(fetched));
        Assert.Equal(FieldNames(created.GetProperty("items")[0]), FieldNames(fetched.GetProperty("items")[0]));

        // enum 一律輸出字串，兩邊都是
        Assert.Equal("Active", created.GetProperty("status").GetString());
        Assert.Equal("Active", fetched.GetProperty("status").GetString());
    }

    [Fact]
    public async Task My_holds_for_a_performance_are_zero_or_one_and_an_unknown_performance_is_404()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.ClientFor(buyers[0]);

        var empty = await (await client.GetAsync($"/api/v1/me/holds?performanceId={Ids.ConcertPerformance}"))
            .ReadJsonAsync();
        Assert.Empty(empty.GetProperty("items").EnumerateArray());

        await client.PostHoldAsync(Ids.ConcertPerformance,
            BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(1, 1)), BookingTestHelpers.NewKey());

        var one = await (await client.GetAsync($"/api/v1/me/holds?performanceId={Ids.ConcertPerformance}"))
            .ReadJsonAsync();
        Assert.Single(one.GetProperty("items").EnumerateArray());

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/v1/me/holds?performanceId=999")).StatusCode);
    }

    [Fact]
    public async Task Orders_are_listed_newest_first_and_the_detail_carries_the_tickets()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.ClientFor(buyers[0]);

        // 兩張訂單：不同場次，避免撞到每人上限
        var firstOrder = await BuyAsync(client, Ids.ConcertPerformance, Ids.ConcertSectionA,
                                        Ids.ConcertSeat(1, 1), Ids.ConcertSeat(1, 2));
        var secondOrder = await BuyAsync(client, Ids.SportPerformance, Ids.SportSectionA,
                                         Ids.SportSeat(1, 1));

        var list = await (await client.GetAsync("/api/v1/orders")).ReadJsonAsync();

        Assert.Equal(2, list.GetProperty("total").GetInt32());
        var items = list.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(secondOrder, items[0].GetProperty("id").GetGuid());       // 新的在前
        Assert.Equal(firstOrder, items[1].GetProperty("id").GetGuid());
        Assert.Equal(2, items[1].GetProperty("quantity").GetInt32());

        // 列表不含明細——它不需要
        Assert.DoesNotContain("items", FieldNames(items[0]));

        var detail = await (await client.GetAsync($"/api/v1/orders/{firstOrder}")).ReadJsonAsync();
        var ticket = detail.GetProperty("items")[0];

        Assert.Equal(["rowNumber", "seatNumber", "sectionCode", "ticketCode", "unitPrice"],
                     FieldNames(ticket));
        Assert.DoesNotContain("seatId", FieldNames(ticket));
        Assert.DoesNotContain("buyerId", FieldNames(detail));
        Assert.Matches("^[0-9a-f]{32}-[0-9]{2}$", ticket.GetProperty("ticketCode").GetString());
    }

    [Theory]
    [InlineData("/api/v1/orders?page=0")]
    [InlineData("/api/v1/orders?pageSize=99")]
    public async Task Bad_paging_is_a_400(string url)
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(1);
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.ClientFor(buyers[0]);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ValidationFailed", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    private static async Task<Guid> BuyAsync(HttpClient client, int performanceId, int sectionId,
                                             params int[] seatIds)
    {
        var hold = await client.PostHoldAsync(performanceId,
            BookingTestHelpers.Manual(sectionId, seatIds), BookingTestHelpers.NewKey());
        var order = await client.CheckoutAsync(await hold.HoldIdAsync(), "Succeeded", BookingTestHelpers.NewKey());

        return (await order.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    private static string[] FieldNames(JsonElement element)
        => element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
}
