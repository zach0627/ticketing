using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using Ticketing.Application.Abstractions;
using Ticketing.Domain.Common;

namespace Ticketing.Infrastructure.Security;

/// <summary>
/// 驗證 Google 簽發的 ID token。
///
/// <c>GoogleJsonWebSignature.ValidateAsync</c> 會去抓 Google 的公鑰，驗簽章、
/// 有效期，並比對 audience 是不是我們的 ClientId。
/// **audience 一定要驗**：少了它，任何人拿別的網站發給自己的 Google token 就能登入我們的系統。
/// </summary>
public sealed class GoogleTokenVerifier(IOptions<GoogleAuthOptions> options) : IGoogleTokenVerifier
{
    public async Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.Enabled)
            throw new BookingRuleException(ErrorCode.Unauthorized, "本站目前未開放 Google 登入");

        ct.ThrowIfCancellationRequested();

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [o.ClientId] });
        }
        catch (InvalidJwtException)
        {
            // 只把「這個 token 不合法」轉成 401。網路失敗之類的例外要原樣往上，
            // 不能把系統問題冒充成使用者的憑證問題。
            throw new BookingRuleException(ErrorCode.Unauthorized, "Google 憑證無效");
        }

        return ToIdentity(payload);
    }

    /// <summary>
    /// Google 給的欄位不一定符合我們資料表的限制（Email 254、subject 64、顯示名 80），
    /// 所以在邊界就檢查與截斷，不要讓外部資料直接決定我們的欄位長度。
    /// </summary>
    private static GoogleIdentity ToIdentity(GoogleJsonWebSignature.Payload payload)
    {
        if (string.IsNullOrWhiteSpace(payload.Subject) || payload.Subject.Length > 64
            || payload.EmailVerified is not true
            || string.IsNullOrWhiteSpace(payload.Email) || payload.Email.Length > 254)
            throw new BookingRuleException(ErrorCode.Unauthorized, "Google 憑證缺少有效身份資料");

        var displayName = string.IsNullOrWhiteSpace(payload.Name) ? "Google 使用者" : payload.Name.Trim();
        if (displayName.Length > 80) displayName = displayName[..80];

        return new GoogleIdentity(payload.Subject, payload.Email, displayName);
    }
}
