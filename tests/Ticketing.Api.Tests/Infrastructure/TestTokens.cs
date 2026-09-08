using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Ticketing.Application.Abstractions;

namespace Ticketing.Api.Tests.Infrastructure;

/// <summary>
/// 自己簽 token，用來測「哪些 token 應該被拒絕」。
///
/// 這些案例**沒辦法**用正常登入產生：過期的、簽章換人的、issuer 錯的、演算法換成
/// HS512 的、沒有 sub 的——正常流程永遠不會發出這種東西，
/// 所以要有一個能故意做壞的工廠（設計文件 10 第 2.2 節 H01）。
/// </summary>
internal static class TestTokens
{
    private static readonly JsonWebTokenHandler Handler = new();

    public static string Create(
        string? signingKey = null,
        string? issuer = null,
        string? audience = null,
        Guid? subject = null,
        string role = "Customer",
        TimeSpan? lifetime = null,
        TimeSpan? issuedAgo = null,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var now = DateTime.UtcNow - (issuedAgo ?? TimeSpan.Zero);

        var claims = new List<Claim>
        {
            new(JwtClaims.Email, "forged@example.com"),
            new(JwtClaims.Name, "偽造者"),
            new(JwtClaims.Role, role)
        };

        // subject 傳 null 代表「刻意不放 sub」，用來驗 OnTokenValidated 的守衛
        if (subject is { } id) claims.Add(new Claim(JwtClaims.Subject, id.ToString()));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(signingKey ?? TestSecurity.SigningKey));

        return Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer ?? TestSecurity.Issuer,
            Audience = audience ?? TestSecurity.Audience,
            NotBefore = now,
            Expires = now + (lifetime ?? TimeSpan.FromMinutes(120)),
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(key, algorithm)
        });
    }
}
