using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Api.Auth;
using Ticketing.Api.Tests.Infrastructure;
using Ticketing.Infrastructure.Persistence;

namespace Ticketing.Api.Tests;

/// <summary>
/// H04 的一部分：容器組得起來、設定錯了要**清楚地**啟動失敗，以及限流真的會擋。
///
/// 這一組**不需要資料庫**：連線字串只是拿來讓 DbContext 註冊得起來，
/// 沒有任何一個測試發出查詢——設定驗證與限流都發生在碰到資料庫之前。
/// </summary>
public class HostStartupTests
{
    private const string UnusedConnectionString =
        "Server=(local);Database=none;Trusted_Connection=True;TrustServerCertificate=True";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SQL_resilience_falls_back_to_the_local_defaults_when_nothing_is_configured()
    {
        using var factory = new TicketingApiFactory(UnusedConnectionString);

        var effective = factory.Services.GetRequiredService<SqlResilienceOptions>();

        // 本機的數字：瞬時錯誤重試兩次就好，撐不過就該讓它失敗
        Assert.Equal(10, effective.CommandTimeoutSeconds);
        Assert.Equal(2, effective.MaxRetryCount);
        Assert.Equal(2, effective.MaxRetryDelaySeconds);
    }

    [Fact]
    public void SQL_resilience_reads_the_Database_section_when_it_is_present()
    {
        // 這個測試守的是一個**安靜的**失敗：設定綁定壞掉不會拋例外，
        // 只會退回預設值，然後在雲端變成「閒置後第一個訪客拿到 500」。
        // 這次雲端上就是先誤判成「重試不夠久」，其實是「連線逾時不算可重試」。
        using var factory = new TicketingApiFactory(
            UnusedConnectionString,
            extraSettings: new Dictionary<string, string?>
            {
                ["Database:CommandTimeoutSeconds"] = "15",
                ["Database:MaxRetryCount"] = "2",
                ["Database:MaxRetryDelaySeconds"] = "5",
            });

        var effective = factory.Services.GetRequiredService<SqlResilienceOptions>();

        Assert.Equal(15, effective.CommandTimeoutSeconds);
        Assert.Equal(2, effective.MaxRetryCount);
        Assert.Equal(5, effective.MaxRetryDelaySeconds);
    }

    [Fact]
    public void Connection_timeouts_are_explicitly_opted_into_as_retryable()
    {
        // EF 預設**不**重試 -2，而且那個預設是對的：逾時的命令可能已經成功了。
        // 這裡刻意加回來，靠的是寫入路徑的單一交易 ＋ 冪等指紋（階段六）。
        // 這個斷言的用意是：哪天有人把它拿掉，要在這裡先紅燈，
        // 而不是等到某次雲端冷啟動才發現。
        Assert.Contains(-2, SqlResilienceOptions.AdditionalTransientErrorNumbers);
    }

    [Fact]
    public void The_container_builds_with_scope_validation_on_and_CurrentUser_resolves()
    {
        // ValidateOnBuild ＋ ValidateScopes 開著：漏註冊、或 Singleton 抓了 Scoped，這一行就會炸
        using var factory = new TicketingApiFactory(UnusedConnectionString);

        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CurrentUser>());
    }

    [Theory]
    [InlineData("Jwt:SigningKey", "", "SigningKey")]                       // 沒設定
    [InlineData("Jwt:SigningKey", "too-short-key", "32")]                  // 短於 HS256 的最小長度
    [InlineData("Jwt:SigningKey", "YOUR_RANDOM_32_PLUS_BYTES_HERE_XXXX", "佔位字串")]  // 範本沒改
    [InlineData("Jwt:Issuer", "", "Issuer")]
    [InlineData("Google:ClientId", "", "Google:ClientId")]
    public void A_bad_security_setting_stops_startup_with_a_message_that_names_the_key(
        string key, string value, string expectedInMessage)
    {
        // 這是刻意的取捨：少了簽章金鑰的 API **不該**啟動成功，
        // 再在第一個人登入時才回 500。錯誤要出現在部署 log 的最上面（設計文件 05 第 6 節）。
        using var factory = new TicketingApiFactory(
            UnusedConnectionString,
            extraSettings: new Dictionary<string, string?> { [key] = value });

        var error = Assert.ThrowsAny<Exception>(() => _ = factory.Services);

        Assert.Contains(expectedInMessage, Flatten(error), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_auth_rate_limit_returns_429_with_a_positive_retry_after()
    {
        // 上限調到 3，第 4 次就該被擋。正式值是每分鐘 10 次（設計文件 05 第 7 節）。
        using var factory = new TicketingApiFactory(
            UnusedConnectionString,
            extraSettings: new Dictionary<string, string?>
            {
                ["RateLimiting:Enabled"] = "true",
                ["RateLimiting:Auth:PermitLimit"] = "3"
            });
        using var client = factory.CreateClient();

        // 送空 body：模型驗證會回 400，但**限流在進到 Controller 之前就算過了**，
        // 所以不必碰資料庫也測得到節流。
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { }, Json);
            statuses.Add(response.StatusCode);

            if (response.StatusCode is not HttpStatusCode.TooManyRequests) continue;

            // 跨網域的前端要讀得到這個 header，CORS 已經 WithExposedHeaders("Retry-After")
            var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds
                             ?? double.Parse(response.Headers.GetValues("Retry-After").First(),
                                             System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(retryAfter > 0, "Retry-After 必須是正整數秒");

            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
            Assert.Equal("RateLimited", problem.GetProperty("code").GetString());
        }

        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.BadRequest));
        Assert.Equal(2, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task Rate_limiting_only_covers_the_auth_endpoints()
    {
        using var factory = new TicketingApiFactory(
            UnusedConnectionString,
            extraSettings: new Dictionary<string, string?>
            {
                ["RateLimiting:Enabled"] = "true",
                ["RateLimiting:Auth:PermitLimit"] = "1"
            });
        using var client = factory.CreateClient();

        // /me 沒掛限流政策：登入的節流額度是為了擋暴力破解，不該連累一般查詢
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
    }

    private static string Flatten(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
            messages.Add(current.Message);
        return string.Join(" | ", messages);
    }
}
