using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Ticketing.Api.ExceptionHandling;
using Ticketing.Application.Abstractions;
using Ticketing.Domain.Common;

namespace Ticketing.Api.RateLimiting;

/// <summary>政策名稱。字串寫死在 Controller 上很容易打錯，集中一份。</summary>
public static class RateLimitPolicies
{
    /// <summary><c>/api/v1/auth/*</c>：每個來源 IP 每分鐘 N 次（設計文件 05 第 7 節）。</summary>
    public const string Auth = "auth";

    /// <summary>保留、付款、取消：每個登入者每分鐘 N 次。</summary>
    public const string BookingWrite = "booking-write";

    /// <summary>暫停售票與重置：每個管理者每分鐘 N 次。額度刻意很小。</summary>
    public const string AdminWrite = "admin-write";
}

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>整合測試預設關掉；限流本身由專門的測試用極低上限驗證。</summary>
    public bool Enabled { get; set; } = true;

    public AuthRateLimitOptions Auth { get; set; } = new();

    public BookingRateLimitOptions Booking { get; set; } = new();

    public AdminRateLimitOptions Admin { get; set; } = new();
}

public sealed class AuthRateLimitOptions
{
    [Range(1, 10_000)]
    public int PermitLimit { get; set; } = 10;
}

public sealed class BookingRateLimitOptions
{
    [Range(1, 10_000)]
    public int PermitLimit { get; set; } = 30;
}

public sealed class AdminRateLimitOptions
{
    /// <summary>重置會刪掉所有購買資料，不該有人每分鐘按幾十次。</summary>
    [Range(1, 10_000)]
    public int PermitLimit { get; set; } = 5;
}

public static class RateLimiterSetup
{
    /// <summary>
    /// 限流。
    ///
    /// 這是**單一 API 程序**的基本節流：兩個程序各有自己的計數器，
    /// 所以它擋的是「同一個人狂打登入」，不宣稱能擋分散式暴力攻擊（設計文件 05 第 7 節）。
    ///
    /// booking 與 admin 的政策在階段 6／7 加入；現在只掛 auth。
    /// </summary>
    public static IServiceCollection AddTicketingRateLimiter(this IServiceCollection services,
                                                             IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<RateLimitOptions>()
                .Bind(configuration.GetSection(RateLimitOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        return services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitPolicies.Auth, context =>
            {
                // ⚠️ 這一行**故意**寫在 partitioner 裡面，不是外面。
                // 寫在外面等於在「註冊服務的當下」就把設定值定死；
                // 那時 WebApplicationFactory 之類的來源還沒有機會覆寫設定，
                // 於是測試設的上限完全沒有作用——而且看起來像限流壞掉。
                // 這裡每個請求解析一次已經合併完成的 Options（單例，成本可忽略）。
                var limits = context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;

                return limits.Enabled
                    ? RateLimitPartition.GetFixedWindowLimiter(PartitionByClientIp(context), _ =>
                        new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = limits.Auth.PermitLimit,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0        // 不排隊：擋下來就直接回 429，不要讓請求卡著
                        })
                    : RateLimitPartition.GetNoLimiter<string>("disabled");
            });

            // 購票寫入按**登入者**分流，不是按 IP：同一個辦公室的人共用出口 IP，
            // 按 IP 分會讓他們互相排擠。走到這裡的請求都通過了 [Authorize]。
            options.AddPolicy(RateLimitPolicies.BookingWrite, context =>
            {
                var limits = context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;

                return limits.Enabled
                    ? RateLimitPartition.GetFixedWindowLimiter(PartitionByUser(context), _ =>
                        new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = limits.Booking.PermitLimit,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        })
                    : RateLimitPartition.GetNoLimiter<string>("disabled");
            });

            // 管理寫入額度刻意很小：重置是破壞性操作
            options.AddPolicy(RateLimitPolicies.AdminWrite, context =>
            {
                var limits = context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;

                return limits.Enabled
                    ? RateLimitPartition.GetFixedWindowLimiter(PartitionByUser(context), _ =>
                        new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = limits.Admin.PermitLimit,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        })
                    : RateLimitPartition.GetNoLimiter<string>("disabled");
            });

            options.OnRejected = async (context, _) =>
            {
                // 順序有意義：Retry-After 要在寫 body 之前設，body 一開始寫就不能再加 header。
                context.HttpContext.Response.Headers.RetryAfter =
                    RetryAfterSeconds(context.Lease).ToString(CultureInfo.InvariantCulture);

                var problems = context.HttpContext.RequestServices.GetRequiredService<ApiProblemWriter>();

                // writer 內部用 HttpContext.RequestAborted，不需要另外傳 token
                await problems.WriteAsync(context.HttpContext, ErrorCode.RateLimited, "請稍後再試");
            };
        });
    }

    /// <summary>
    /// 用連線的來源 IP 分流。
    ///
    /// **不讀 X-Forwarded-For**：那是使用者可以隨便填的 header，
    /// 相信它等於讓每個請求自稱來自不同 IP，限流形同虛設。
    /// 上雲之後要改成「只接受已知代理提供的 forwarded headers」再取 RemoteIpAddress（階段 8）。
    /// </summary>
    private static string PartitionByClientIp(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>登入者的 <c>sub</c>；理論上不會是匿名（端點有 <c>[Authorize]</c>），但仍給一個保底分區。</summary>
    private static string PartitionByUser(HttpContext context)
        => context.User.FindFirstValue(JwtClaims.Subject) ?? PartitionByClientIp(context);

    /// <summary>固定視窗會給出下一次可用的時間；拿不到就保守回 60 秒，而且一定是正整數。</summary>
    private static int RetryAfterSeconds(RateLimitLease lease)
        => lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 60;
}
