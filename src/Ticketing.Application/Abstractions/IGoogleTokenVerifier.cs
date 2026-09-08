namespace Ticketing.Application.Abstractions;

/// <summary>
/// 隔開 <c>Google.Apis.Auth</c>。實作要驗簽章、audience 與有效期；
/// 失敗一律轉成 <c>Unauthorized</c>，不要把套件的例外型別漏到 Application（設計文件 05 第 4.2 節）。
/// </summary>
public interface IGoogleTokenVerifier
{
    Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken ct);
}

/// <summary>
/// 驗證通過後我們願意相信的三件事。
///
/// <paramref name="Subject"/> 是 Google 的 <c>sub</c>，**永遠不變**，才是身份主鍵；
/// Email 可以換、可以被轉讓，所以即使 <c>email_verified=true</c> 也不拿它自動合併帳號。
/// </summary>
public sealed record GoogleIdentity(string Subject, string Email, string DisplayName);
