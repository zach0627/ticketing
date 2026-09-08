using Ticketing.Application.Abstractions;
using Ticketing.Domain.Common;

namespace Ticketing.Api.Tests.Infrastructure;

/// <summary>
/// 假的 Google 驗簽。
///
/// **這裡刻意不測 Google 的簽章驗證**：那需要連到 Google 的公鑰伺服器、
/// 而且我們也沒有能力簽出一個 Google 會承認的 token。那一段由
/// <c>Google.Apis.Auth</c> 負責，並在瀏覽器上人工驗證一次（設計文件 05 第 10 節）。
///
/// 這個替身讓我們能測**我們自己寫的那一半**：subject 對應帳號、Email 撞號、
/// 兩個併發初次登入只會產生一個帳號。輸入格式是 <c>subject|email|displayName</c>。
/// </summary>
public sealed class StubGoogleTokenVerifier : IGoogleTokenVerifier
{
    public Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken ct)
    {
        var parts = (idToken ?? "").Split('|');

        if (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace))
            throw new BookingRuleException(ErrorCode.Unauthorized, "Google 憑證無效");

        return Task.FromResult(new GoogleIdentity(parts[0], parts[1], parts[2]));
    }
}
