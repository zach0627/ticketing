using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ticketing.Infrastructure.Persistence;

namespace Ticketing.Api.Tests.Infrastructure;

/// <summary>
/// 測試用的簽章設定。
///
/// **不寫死字串**：金鑰每次執行隨機產生，所以原始碼裡根本沒有一個看起來像金鑰的東西，
/// 也證明了程式真的是從設定讀金鑰、不是靠某個常數（設計文件 22 第 3 節）。
/// </summary>
public static class TestSecurity
{
    public static string SigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>用來測「別人簽的 token 不能用」。</summary>
    public static string OtherSigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public const string Issuer = "ticketing-tests";
    public const string Audience = "ticketing-tests";
    public const string GoogleClientId = "test-client.apps.googleusercontent.com";
}

/// <summary>
/// 用真的 <c>Program</c> 啟動 API，只把資料庫換成測試專用的那一個。
///
/// **刻意不換掉任何其他東西**：Controller、Queries、對應器、錯誤處理、JSON 設定、
/// 認證與授權全部是正式的那一套，測到的才是真的行為（設計文件 10 第 1.2 節）。
/// 唯一的例外由 <paramref name="overrideServices"/> 明確指定，目前只用在 Google 驗簽——
/// 那一段要連 Google 的公鑰伺服器，不屬於自動化測試能保證的範圍。
///
/// （階段 4 時叫 CatalogApiFactory；階段 5 起同時服務 auth，改成中性的名字。）
/// </summary>
public sealed class TicketingApiFactory(
    string connectionString,
    Action<IServiceCollection>? overrideServices = null,
    IDictionary<string, string?>? extraSettings = null,
    IInterceptor? interceptor = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(configuration =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = TestSecurity.Issuer,
                ["Jwt:Audience"] = TestSecurity.Audience,
                ["Jwt:SigningKey"] = TestSecurity.SigningKey,
                ["Jwt:AccessTokenMinutes"] = "120",
                ["Google:Enabled"] = "true",
                ["Google:ClientId"] = TestSecurity.GoogleClientId,

                // 預設關限流：一個測試檔跑幾十次登入，開著會互相干擾。
                // 限流本身由 RateLimitTests 用極低上限單獨驗（設計文件 05 第 7 節）。
                ["RateLimiting:Enabled"] = "false"
            };

            if (extraSettings is not null)
                foreach (var (key, value) in extraSettings) settings[key] = value;

            configuration.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            // 先移除原本的 DbContext 與它的 options，避免同時連到本機開發庫
            services.RemoveAll<DbContextOptions<TicketingDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<TicketingDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<TicketingDbContext>>();

            services.AddDbContext<TicketingDbContext>(o =>
            {
                // 重試的錯誤清單跟正式註冊用同一份常數——這裡如果自己寫一份，
                // 測試就會在一個「跟正式環境不一樣的韌性設定」上跑
                o.UseSqlServer(connectionString,
                    sql => sql.CommandTimeout(10)
                              .EnableRetryOnFailure(2, TimeSpan.FromSeconds(2),
                                                    SqlResilienceOptions.AdditionalTransientErrorNumbers));

                // T21 用：在 commit 前後注入故障，模擬「交易成功但回覆遺失」與「commit 前掛掉」
                if (interceptor is not null) o.AddInterceptors(interceptor);
            });

            // 讓 MVC 找得到測試專案裡的探針 Controller（見 AuthProbeController 的說明）
            services.AddControllers()
                    .PartManager.ApplicationParts.Add(new AssemblyPart(typeof(AuthProbeController).Assembly));

            overrideServices?.Invoke(services);
        });
    }
}
