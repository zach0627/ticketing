using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ticketing.Api.ExceptionHandling;
using Ticketing.Application.Abstractions;
using Ticketing.Domain.Common;
using Ticketing.Infrastructure.Security;

namespace Ticketing.Api.Auth;

/// <summary>
/// 設定 JwtBearer 的驗證規則與三個事件。
///
/// 為什麼是 <see cref="IConfigureNamedOptions{TOptions}"/> 而不是在 <c>Program.cs</c>
/// 直接寫 <c>AddJwtBearer(o =&gt; ...)</c>？因為那樣要在組容器的當下就把 <c>Jwt:SigningKey</c>
/// 讀出來，等於繞過 <c>ValidateOnStart</c>：設定漏了會得到一個
/// <c>ArgumentNullException</c>，而不是「Jwt:SigningKey 是必填」這種看得懂的訊息。
///
/// 走 Options 就能注入已驗證的 <see cref="JwtOptions"/>，順便讓 <see cref="ApiProblemWriter"/>
/// 也用 DI 拿到（設計文件 05 第 4.2 節、13 第 3 節）。
/// </summary>
public sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions, ApiProblemWriter problems)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name == JwtBearerDefaults.AuthenticationScheme) Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var jwt = jwtOptions.Value;   // 這一行會觸發 Options 驗證：缺設定就是清楚的啟動失敗

        // ⭐ 一定要關掉 claim 映射。預設 JwtBearer 會把 sub 改寫成
        // http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier，
        // 於是 CurrentUser 找 "sub" 會找不到——而且 token 是有效的，症狀完全不像認證問題。
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,

            // 只接受 HS256。不限制演算法就等於接受攻擊者挑一個弱的（或 alg=none）。
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

            NameClaimType = JwtClaims.Name,
            RoleClaimType = JwtClaims.Role,     // [Authorize(Roles = "Admin")] 才讀得到
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            // 簽章對了不代表內容合用：沒有可解析的 sub，後面每個「本人資源」查詢都會失敗。
            // 在這裡擋掉，比讓它一路走到 Service 才爆好。
            OnTokenValidated = context =>
            {
                if (!Guid.TryParse(context.Principal?.FindFirstValue(JwtClaims.Subject), out _))
                    context.Fail("Missing or invalid subject");
                return Task.CompletedTask;
            },

            // 401：沒帶 token、過期、簽章錯。預設會回一個空 body，
            // 前端就拿不到 code 與 traceId——所以接管它（設計文件 13 第 3 節）。
            OnChallenge = async context =>
            {
                context.HandleResponse();     // 停掉預設回應，改由我們寫

                // HandleResponse() 也順便把預設的 WWW-Authenticate 停掉了，自己補回來：
                // 少了它就不是合規的 401 挑戰。
                context.Response.Headers.WWWAuthenticate = context.AuthenticateFailure is null
                    ? "Bearer"
                    : "Bearer error=\"invalid_token\"";

                await problems.WriteAsync(context.HttpContext, ErrorCode.Unauthorized, "請先登入");
            },

            // 403：登入了，但角色不對。
            OnForbidden = context =>
                problems.WriteAsync(context.HttpContext, ErrorCode.Forbidden, "沒有權限執行這個操作")
        };
    }
}
