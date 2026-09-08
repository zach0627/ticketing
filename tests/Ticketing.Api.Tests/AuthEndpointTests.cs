using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Ticketing.Api.Tests.Infrastructure;
using Ticketing.Application.Abstractions;

namespace Ticketing.Api.Tests;

/// <summary>
/// H01：註冊、JWT、Google、授權。真的 HTTP、真的 SQL Server、真的簽發與驗證流程
/// （設計文件 05 第 10 節、10 第 2.2 節）。
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class AuthEndpointTests(SqlServerFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ── 註冊 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Registering_returns_201_with_a_token_and_only_the_public_user_fields()
    {
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = "  New@Example.COM ", password = "password123", displayName = "  新使用者 " }, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));

        var user = body.GetProperty("user");
        Assert.Equal("new@example.com", user.GetProperty("email").GetString());   // 正規化過
        Assert.Equal("新使用者", user.GetProperty("displayName").GetString());     // trim 過
        Assert.Equal("Customer", user.GetProperty("role").GetString());           // enum 是字串

        var fields = user.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(["displayName", "email", "id", "role"], fields);
    }

    [Fact]
    public async Task The_database_never_holds_the_plain_password()
    {
        const string password = "not-in-the-database-1";

        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = "hash@example.com", password, displayName = "雜湊" }, Json);

        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT PasswordHash FROM dbo.AppUsers WHERE Email = 'hash@example.com';";
        var stored = (string?)await command.ExecuteScalarAsync();

        Assert.False(string.IsNullOrWhiteSpace(stored));
        Assert.DoesNotContain(password, stored, StringComparison.Ordinal);
        Assert.StartsWith("AQAAAA", stored, StringComparison.Ordinal);   // ASP.NET Identity 的 PBKDF2 格式
    }

    [Fact]
    public async Task Registering_the_same_email_twice_is_409_even_when_the_case_differs()
    {
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = "dup@example.com", password = "password123", displayName = "第一個" }, Json);

        var second = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = "DUP@Example.com", password = "password123", displayName = "第二個" }, Json);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await AssertProblemAsync(second, "EmailAlreadyRegistered");
    }

    [Fact]
    public async Task Ten_simultaneous_registrations_of_one_email_create_exactly_one_account()
    {
        // 先查 EmailExists 只能改善訊息；真正的保證是 UQ_AppUsers_Email，
        // 而 UnitOfWork 要把那個索引衝突翻譯成 409 而不是 500（設計文件 05 第 4.1 節）。
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(async i =>
        {
            using var client = factory.CreateClient();
            return await client.PostAsJsonAsync("/api/v1/auth/register",
                new { email = "race@example.com", password = "password123", displayName = $"併發 {i}" }, Json);
        }));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(9, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await CountUsersAsync(db, "race@example.com"));

        foreach (var conflict in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            await AssertProblemAsync(conflict, "EmailAlreadyRegistered");
    }

    [Theory]
    [InlineData("not-an-email", "password123", "名字")]      // Email 格式
    [InlineData("short@example.com", "1234567", "名字")]     // 密碼 7 碼
    [InlineData("blank@example.com", "password123", "  ")]   // 顯示名只有空白
    public async Task Bad_registration_input_is_400_before_any_hashing_happens(
        string email, string password, string displayName)
    {
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email, password, displayName }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await AssertProblemAsync(response, "ValidationFailed");
        Assert.True(problem.TryGetProperty("errors", out _));   // 逐欄位錯誤要留著給前端
    }

    // ── 登入與 /me ────────────────────────────────────────────────────

    [Fact]
    public async Task A_real_login_produces_a_token_that_the_api_itself_accepts()
    {
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = "login@example.com", password = "password123", displayName = "登入者" }, Json);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "login@example.com", password = "password123" }, Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("accessToken").GetString();

        // 端到端：簽發與驗證用的是同一組 issuer／audience／金鑰／claim 名稱
        using var authorized = factory.CreateClient();
        authorized.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var me = await authorized.GetFromJsonAsync<JsonElement>("/api/v1/me", Json);
        Assert.Equal("login@example.com", me.GetProperty("email").GetString());
        Assert.Equal("登入者", me.GetProperty("displayName").GetString());
    }

    [Theory]
    [InlineData("login@example.com", "wrong-password")]
    [InlineData("nobody@example.com", "password123")]
    public async Task Wrong_password_and_unknown_account_are_indistinguishable(string email, string password)
    {
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = "login@example.com", password = "password123", displayName = "登入者" }, Json);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await AssertProblemAsync(response, "Unauthorized");
        Assert.Equal("Email 或密碼錯誤", problem.GetProperty("detail").GetString());
    }

    // ── 該被拒絕的 token ──────────────────────────────────────────────

    public static TheoryData<string, string?> RejectedTokens() => new()
    {
        { "沒帶 token", null },
        { "根本不是 JWT", "this.is.not.a.jwt" },
        { "別人的金鑰簽的", TestTokens.Create(signingKey: TestSecurity.OtherSigningKey, subject: Guid.NewGuid()) },
        { "issuer 不對", TestTokens.Create(issuer: "someone-else", subject: Guid.NewGuid()) },
        { "audience 不對", TestTokens.Create(audience: "another-app", subject: Guid.NewGuid()) },
        { "演算法換成 HS512", TestTokens.Create(subject: Guid.NewGuid(),
                                                algorithm: SecurityAlgorithms.HmacSha512) },
        { "已經過期", TestTokens.Create(subject: Guid.NewGuid(),
                                        issuedAgo: TimeSpan.FromHours(4), lifetime: TimeSpan.FromHours(2)) },
        { "沒有 sub", TestTokens.Create(subject: null) }
    };

    [Theory]
    [MemberData(nameof(RejectedTokens))]
    public async Task Protected_endpoints_reject_every_kind_of_bad_token(string reason, string? token)
    {
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
        await AssertProblemAsync(response, "Unauthorized", because: reason);
    }

    [Fact]
    public async Task Ninety_nine_percent_valid_is_still_rejected_a_tampered_signature_does_not_pass()
    {
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        // 改 payload 的最後一個字元：header 與 signature 原封不動 → 簽章對不上
        var token = TestTokens.Create(subject: Guid.NewGuid());
        var parts = token.Split('.');
        parts[1] = parts[1][..^1] + (parts[1][^1] == 'A' ? 'B' : 'A');

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", string.Join('.', parts));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
    }

    // ── 授權：401 / 403 ───────────────────────────────────────────────

    [Fact]
    public async Task A_customer_gets_403_on_an_admin_route_and_an_admin_gets_through()
    {
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);

        using var customer = factory.CreateClient();
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.Create(subject: Guid.NewGuid(), role: "Customer"));

        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestTokens.Create(subject: Guid.NewGuid(), role: "Admin"));

        // 沒登入 → 401；登入了但角色不對 → 403；角色對 → 200
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/test-only/admin-only")).StatusCode);

        var forbidden = await customer.GetAsync("/api/v1/test-only/admin-only");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        await AssertProblemAsync(forbidden, "Forbidden");

        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/v1/test-only/any-user")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/test-only/admin-only")).StatusCode);
    }

    [Fact]
    public async Task Public_catalog_endpoints_stay_public_after_authentication_is_switched_on()
    {
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = new TicketingApiFactory(db.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/events")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    // ── Google ────────────────────────────────────────────────────────

    [Fact]
    public async Task Google_creates_an_account_on_the_first_login_and_reuses_it_afterwards()
    {
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = WithStubbedGoogle(db.ConnectionString);
        using var client = factory.CreateClient();

        var first = await PostGoogleAsync(client, "112233|G@Example.com|Google 使用者");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("user").GetProperty("id").GetString();

        // Google 那邊改了 Email 與顯示名，但 subject 一樣 → 還是同一個帳號
        var second = await PostGoogleAsync(client, "112233|renamed@example.com|改過名字");
        var secondUser = (await second.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("user");

        Assert.Equal(firstId, secondUser.GetProperty("id").GetString());
        Assert.Equal("g@example.com", secondUser.GetProperty("email").GetString());
        Assert.Equal(1, await CountUsersAsync(db, "g@example.com"));
    }

    [Fact]
    public async Task Five_simultaneous_first_google_logins_end_up_on_one_account()
    {
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = WithStubbedGoogle(db.ConnectionString);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            using var client = factory.CreateClient();
            return await PostGoogleAsync(client, "998877|parallel@example.com|同時登入");
        }));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var ids = new List<string?>();
        foreach (var response in responses)
            ids.Add((await response.Content.ReadFromJsonAsync<JsonElement>(Json))
                .GetProperty("user").GetProperty("id").GetString());

        Assert.Single(ids.Distinct());                                  // 五個人拿到同一個帳號
        Assert.Equal(1, await CountUsersAsync(db, "parallel@example.com"));
    }

    [Fact]
    public async Task Google_cannot_take_over_an_email_that_already_registered_with_a_password()
    {
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = WithStubbedGoogle(db.ConnectionString);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/register",
            new { email = "owner@example.com", password = "password123", displayName = "本人" }, Json);

        var response = await PostGoogleAsync(client, "555000|owner@example.com|冒名者");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertProblemAsync(response, "EmailAlreadyRegistered");
        Assert.Equal(1, await CountUsersAsync(db, "owner@example.com"));
    }

    [Fact]
    public async Task An_invalid_google_credential_is_401_not_500()
    {
        await using var db = await fixture.CreateDatabaseAsync();
        using var factory = WithStubbedGoogle(db.ConnectionString);
        using var client = factory.CreateClient();

        var response = await PostGoogleAsync(client, "garbage");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertProblemAsync(response, "Unauthorized");
    }

    // ── helpers ───────────────────────────────────────────────────────

    private static TicketingApiFactory WithStubbedGoogle(string connectionString) =>
        new(connectionString, services =>
        {
            services.RemoveAll<IGoogleTokenVerifier>();
            services.AddSingleton<IGoogleTokenVerifier, StubGoogleTokenVerifier>();
        });

    private static Task<HttpResponseMessage> PostGoogleAsync(HttpClient client, string idToken) =>
        client.PostAsJsonAsync("/api/v1/auth/google", new { idToken }, Json);

    private static async Task<int> CountUsersAsync(TestDatabase db, string email)
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM dbo.AppUsers WHERE Email = @email;";
        command.Parameters.AddWithValue("@email", email);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, string code,
                                                              string? because = null)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()), because);

        return problem;
    }
}
