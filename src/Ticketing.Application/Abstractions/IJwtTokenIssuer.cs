using Ticketing.Domain.Users;

namespace Ticketing.Application.Abstractions;

/// <summary>
/// 隔開 <c>Microsoft.IdentityModel.JsonWebTokens</c>。
/// Application 只知道「拿使用者換一個字串」，不知道 JWT 有三段、用什麼演算法簽（設計文件 05 第 2 節）。
/// </summary>
public interface IJwtTokenIssuer
{
    string Issue(AppUser user);
}

/// <summary>
/// 簽發端（Infrastructure 的 <c>JwtTokenIssuer</c>）與驗證端（Api 的 JwtBearer 設定）
/// 共用的 claim 名稱。
///
/// 為什麼放在 Application？因為那兩層互相看不見，這裡是唯一都看得到的地方。
/// 兩邊各寫一份字串，遲早會不一致——而不一致的症狀是「token 明明有效卻讀不到使用者」，
/// 非常難查（設計文件 05 第 4.2 節）。
/// </summary>
public static class JwtClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}
