using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Infrastructure.Persistence.Seeding;

namespace Ticketing.Api.Tests.Infrastructure;

/// <summary>
/// 併發購票測試的共用工具。
///
/// **座位 ID 是算出來的，不是猜的**（設計文件 15 第 2 節）：
/// <c>座位 = 場次 × 1000 + 區index × 100 + (排−1) × 每排席數 + 座號</c>。
/// seed 是決定性的，所以測試可以直接寫 <c>1101</c>。
/// </summary>
internal static class Ids
{
    /// <summary>演唱會 C01：3 區 × 5 排 × 12 席。</summary>
    public const int ConcertPerformance = 1;
    public const int ConcertSectionA = 11;

    /// <summary>運動賽事 S01：3 區 × 5 排 × 16 席（連號測試用）。</summary>
    public const int SportPerformance = 13;
    public const int SportSectionA = 131;

    public static int ConcertSeat(int row, int number) => 1000 + 100 + (row - 1) * 12 + number;

    public static int SportSeat(int row, int number) => 13 * 1000 + 100 + (row - 1) * 16 + number;
}

internal static class BookingTestHelpers
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>建立資料庫並跑真的 seed（15 場活動、2,880 席、預建帳號）。</summary>
    public static async Task<TestDatabase> SeededDatabaseAsync(this SqlServerFixture fixture,
                                                               bool readCommittedSnapshot = false)
    {
        var database = await fixture.CreateDatabaseAsync(readCommittedSnapshot);
        await using var services = database.BuildServices();
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SeedRunner>().RunAsync(CancellationToken.None);
        return database;
    }

    /// <summary>
    /// 直接用 SQL 建立買家，**不跑 PBKDF2**。
    ///
    /// 50 個帳號各算一次密碼雜湊要好幾秒，而這些測試根本不驗密碼——
    /// 它們用 <see cref="TestTokens"/> 直接簽 token。
    /// <c>PasswordHash</c> 給一個假值只是為了滿足 <c>CK_AppUsers_Identity</c>。
    /// </summary>
    public static async Task<Guid[]> CreateBuyersAsync(this TestDatabase db, int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync();

        for (var i = 0; i < ids.Length; i++)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT dbo.AppUsers (Id, Email, DisplayName, PasswordHash, GoogleSubject, Role, CreatedAtUtc)
                VALUES (@id, @email, @name, 'AQAAAA-test-only-not-a-real-hash', NULL, 'Customer',
                        SYSDATETIMEOFFSET());
                """;
            command.Parameters.AddWithValue("@id", ids[i]);
            command.Parameters.AddWithValue("@email", $"buyer{i}-{ids[i]:N}@example.test");
            command.Parameters.AddWithValue("@name", $"買家 {i}");
            await command.ExecuteNonQueryAsync();
        }

        return ids;
    }

    public static HttpClient ClientFor(this TicketingApiFactory factory, Guid buyerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Create(subject: buyerId));
        return client;
    }

    public static Task<HttpResponseMessage> PostHoldAsync(this HttpClient client, int performanceId,
        object body, string? key = null)
        => client.SendJsonAsync(HttpMethod.Post, $"/api/v1/performances/{performanceId}/holds", body, key);

    public static Task<HttpResponseMessage> CheckoutAsync(this HttpClient client, Guid holdId,
        string outcome, string? key = null)
        => client.SendJsonAsync(HttpMethod.Post, $"/api/v1/holds/{holdId}/checkout",
                                new { outcome }, key);

    public static Task<HttpResponseMessage> CancelAsync(this HttpClient client, Guid holdId)
        => client.SendJsonAsync(HttpMethod.Post, $"/api/v1/holds/{holdId}/cancel", body: null, key: null);

    public static async Task<HttpResponseMessage> SendJsonAsync(this HttpClient client, HttpMethod method,
        string url, object? body, string? key)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);

        return await client.SendAsync(request);
    }

    /// <summary>手選保留的 body。</summary>
    public static object Manual(int sectionId, params int[] seatIds)
        => new { sectionId, quantity = seatIds.Length, selectionMode = "Manual", seatIds };

    public static object Contiguous(int sectionId, int quantity)
        => new { sectionId, quantity, selectionMode = "Contiguous", seatIds = Array.Empty<int>() };

    public static string NewKey() => Guid.NewGuid().ToString("D");

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>(Json);

    public static async Task<Guid> HoldIdAsync(this HttpResponseMessage response)
        => (await response.ReadJsonAsync()).GetProperty("id").GetGuid();

    // ── 直接查資料庫：驗「資料對」，不是只驗「HTTP 沒爆」 ──────────────

    public static async Task<T> ScalarAsync<T>(this TestDatabase db, string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default! : (T)result;
    }

    public static Task<string> SeatStatusAsync(this TestDatabase db, int seatId)
        => db.ScalarAsync<string>("SELECT Status FROM dbo.Seats WHERE Id = @id;", ("@id", seatId));

    public static Task<int> CountAsync(this TestDatabase db, string table, string where = "1 = 1",
        params (string Name, object Value)[] parameters)
        => db.ScalarAsync<int>($"SELECT COUNT(*) FROM dbo.{table} WHERE {where};", parameters);

    /// <summary>
    /// 用 SQL 直接把一批座位變成「別人有效的保留」，當作測試的前置佈景。
    ///
    /// 為什麼不用 API 建？因為要把某一排卡成「沒有連續 6 席」得同時佔住好幾個不相鄰的位子，
    /// 而一個買家在同一場只能有一筆有效保留——用 API 得開一堆帳號，慢又難讀。
    /// 這裡建立的資料完全符合外鍵與 CHECK（Held 必須同時有 HoldId 與 HeldUntilUtc）。
    ///
    /// <paramref name="holdFor"/> 給**負值**就會做出一筆「資料庫還是 Active、但時間早就過了」的保留——
    /// 這正是沒有背景清理程式時的真實狀態。<c>CreatedAtUtc</c> 要跟著往前推，
    /// 否則會撞到 <c>CK_SeatHolds_Expiry</c>（到期必須晚於建立）。
    /// </summary>
    public static async Task<Guid> BlockSeatsAsync(this TestDatabase db, int performanceId, Guid buyerId,
        TimeSpan holdFor, params int[] seatIds)
    {
        var holdId = Guid.NewGuid();

        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync();

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = """
                DECLARE @expires datetimeoffset(7) = DATEADD(second, @seconds, SYSDATETIMEOFFSET());

                INSERT dbo.SeatHolds (Id, BuyerId, PerformanceId, Status, CreatedAtUtc, ExpiresAtUtc, TotalAmount)
                VALUES (@id, @buyer, @performance, 'Active',
                        DATEADD(minute, -5, @expires),      -- 跟真實保留一樣：五分鐘前建立的
                        @expires, 100);
                """;
            insert.Parameters.AddWithValue("@id", holdId);
            insert.Parameters.AddWithValue("@buyer", buyerId);
            insert.Parameters.AddWithValue("@performance", performanceId);
            insert.Parameters.AddWithValue("@seconds", (int)holdFor.TotalSeconds);
            await insert.ExecuteNonQueryAsync();
        }

        await using var update = connection.CreateCommand();
        update.CommandText = $"""
            UPDATE dbo.Seats
            SET Status = 'Held', HoldId = @id,
                HeldUntilUtc = DATEADD(second, @seconds, SYSDATETIMEOFFSET())
            WHERE Id IN ({string.Join(",", seatIds)});
            """;
        update.Parameters.AddWithValue("@id", holdId);
        update.Parameters.AddWithValue("@seconds", (int)holdFor.TotalSeconds);
        await update.ExecuteNonQueryAsync();

        return holdId;
    }
}
