using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Ticketing.Application.Abstractions;
using Ticketing.Domain.Users;

namespace Ticketing.Infrastructure.Security;

/// <summary>
/// 簽發 JWT。JWT 的三段是 header.payload.signature，
/// 前兩段只是 **base64url，不是加密**——任何人都讀得到。
/// 所以 payload 只放身份與角色，絕不放密碼、金鑰或 Google 的 ID token（設計文件 05 第 4.2 節）。
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options) : IJwtTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public string Issue(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var o = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey));

        // ⚠️ 這裡刻意用 TimeProvider.System，不是注入的業務時鐘。
        // 憑證的 nbf／exp 必須跟驗證端（JwtBearer 用真實時間）對得上；
        // 測試把業務時間撥到三天後時，token 不該跟著變成三天後才生效。
        // 「過期 token」的測試另外簽一個相對真實現在已經過期的 token，
        // 而不是關掉 lifetime 驗證（設計文件 05 第 4.2 節）。
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            NotBefore = now,
            Expires = now.AddMinutes(o.AccessTokenMinutes),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtClaims.Subject, user.Id.ToString()),
                new Claim(JwtClaims.Email, user.Email),
                new Claim(JwtClaims.Name, user.DisplayName),

                // 角色放在 token 裡的代價：改了角色，舊 token 在有效期內仍持有舊權限。
                // 本專案接受這個限制，升權後重新登入即可（設計文件 05 第 4.2 節）。
                new Claim(JwtClaims.Role, user.Role.ToString())
            ]),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        return _handler.CreateToken(descriptor);
    }
}
