using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Api.Tests.Infrastructure;
using Ticketing.Infrastructure.Persistence.Seeding;

namespace Ticketing.Api.Tests;

/// <summary>
/// 第一條垂直切片的 HTTP 驗收：瀏覽器 → Controller → Queries → 真 SQL Server。
/// 同時驗公開端點的錯誤契約（設計文件 13 第 3 節）。
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class CatalogEndpointTests(SqlServerFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Event_list_returns_all_fifteen_events_with_computed_fields()
    {
        await using var db = await SeededDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/events?pageSize=30", Json);

        Assert.Equal(15, page.GetProperty("total").GetInt32());
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(15, items.Count);

        var first = items[0];
        Assert.Equal("TWD", first.GetProperty("currency").GetString());
        Assert.True(first.GetProperty("minPrice").GetDecimal() > 0);
        // enum 一律輸出字串，不是數字
        Assert.Equal("OnSale", first.GetProperty("salesStatus").GetString());
        Assert.True(first.TryGetProperty("serverNowUtc", out _));
    }

    [Fact]
    public async Task Event_list_filters_by_category_and_pages()
    {
        await using var db = await SeededDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        var concerts = await client.GetFromJsonAsync<JsonElement>("/api/v1/events?category=Concert&pageSize=30", Json);
        Assert.Equal(12, concerts.GetProperty("total").GetInt32());

        var sports = await client.GetFromJsonAsync<JsonElement>("/api/v1/events?category=Sport&pageSize=30", Json);
        Assert.Equal(3, sports.GetProperty("total").GetInt32());

        // total 是符合條件的總數，不是本頁筆數
        var firstPage = await client.GetFromJsonAsync<JsonElement>("/api/v1/events?page=1&pageSize=5", Json);
        Assert.Equal(15, firstPage.GetProperty("total").GetInt32());
        Assert.Equal(5, firstPage.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Event_detail_carries_the_purchase_policy_from_the_domain()
    {
        await using var db = await SeededDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        var concert = await client.GetFromJsonAsync<JsonElement>("/api/v1/events/C01", Json);
        Assert.Equal(4, concert.GetProperty("maxTicketsPerBuyer").GetInt32());
        Assert.False(concert.GetProperty("allowsContiguousAllocation").GetBoolean());
        Assert.Equal(3, concert.GetProperty("sections").GetArrayLength());

        var sport = await client.GetFromJsonAsync<JsonElement>("/api/v1/events/S01", Json);
        Assert.Equal(6, sport.GetProperty("maxTicketsPerBuyer").GetInt32());
        Assert.True(sport.GetProperty("allowsContiguousAllocation").GetBoolean());
    }

    [Fact]
    public async Task Seat_map_never_leaks_hold_or_buyer_information()
    {
        await using var db = await SeededDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        var map = await client.GetFromJsonAsync<JsonElement>("/api/v1/performances/1/seats", Json);

        Assert.Equal(180, map.GetProperty("seats").GetArrayLength());     // 演唱會 3 區 × 5 排 × 12 席

        var seat = map.GetProperty("seats")[0];
        var fields = seat.EnumerateObject().Select(p => p.Name).ToArray();

        // 公開端點只給這五個欄位——holdId、heldUntilUtc、買家一律不外洩
        Assert.Equal(["id", "rowNumber", "seatNumber", "sectionId", "status"], fields.Order().ToArray());
        Assert.DoesNotContain("holdId", fields, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("heldUntilUtc", fields, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("buyerId", fields, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Seat_map_shows_an_expired_hold_as_available_but_a_live_one_as_held()
    {
        // 「過期的 Held 視同可用」這條規則寫在三個地方：Seat.IsAvailableAt（記憶體）、
        // 座位圖投影（畫面）、SeatRepository.TryHoldAsync 的 WHERE（真正決定歸屬）。
        // 這個測試綁住前兩者一致；第三者要等階段 6 的 T08。
        await using var db = await SeededDatabaseAsync();

        await using (var connection = new Microsoft.Data.SqlClient.SqlConnection(db.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @buyer uniqueidentifier = (SELECT TOP 1 Id FROM dbo.AppUsers ORDER BY Email);
                DECLARE @expired uniqueidentifier = NEWID(), @live uniqueidentifier = NEWID();
                DECLARE @now datetimeoffset(7) = SYSDATETIMEOFFSET();

                INSERT dbo.SeatHolds(Id,BuyerId,PerformanceId,Status,CreatedAtUtc,ExpiresAtUtc,TotalAmount)
                VALUES(@expired,@buyer,1,'Cancelled',DATEADD(hour,-1,@now),DATEADD(minute,-55,@now),2100),
                      (@live,@buyer,1,'Active',@now,DATEADD(minute,5,@now),2100);

                -- 1101：保留已過期（HeldUntilUtc 在過去）→ 對外應顯示 Available
                UPDATE dbo.Seats SET Status='Held', HoldId=@expired, HeldUntilUtc=DATEADD(minute,-55,@now)
                WHERE Id=1101;

                -- 1102：保留還有效 → 對外應顯示 Held
                UPDATE dbo.Seats SET Status='Held', HoldId=@live, HeldUntilUtc=DATEADD(minute,5,@now)
                WHERE Id=1102;
                """;
            await command.ExecuteNonQueryAsync();
        }

        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        var map = await client.GetFromJsonAsync<JsonElement>("/api/v1/performances/1/seats", Json);
        var seats = map.GetProperty("seats").EnumerateArray()
            .ToDictionary(s => s.GetProperty("id").GetInt32(), s => s.GetProperty("status").GetString());

        Assert.Equal("Available", seats[1101]);   // 過期的 Held 視同可用
        Assert.Equal("Held", seats[1102]);        // 有效的保留維持 Held
        Assert.Equal("Available", seats[1103]);   // 沒被動過的
    }

    [Theory]
    [InlineData("/api/v1/events/XXX", HttpStatusCode.NotFound, "NotFound")]
    [InlineData("/api/v1/performances/999/seats", HttpStatusCode.NotFound, "NotFound")]
    [InlineData("/api/v1/events?pageSize=99", HttpStatusCode.BadRequest, "ValidationFailed")]
    [InlineData("/api/v1/events?page=0", HttpStatusCode.BadRequest, "ValidationFailed")]
    [InlineData("/api/v1/events?category=NotAThing", HttpStatusCode.BadRequest, "ValidationFailed")]
    [InlineData("/api/v1/nope", HttpStatusCode.NotFound, "NotFound")]
    public async Task Errors_always_carry_a_code_and_a_trace_id(string url, HttpStatusCode expected, string code)
    {
        await using var db = await SeededDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Health_check_answers_without_touching_the_database()
    {
        await using var db = await SeededDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<TestDatabase> SeededDatabaseAsync()
    {
        var database = await fixture.CreateDatabaseAsync();
        await using var services = database.BuildServices();
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SeedRunner>().RunAsync(CancellationToken.None);
        return database;
    }
}
