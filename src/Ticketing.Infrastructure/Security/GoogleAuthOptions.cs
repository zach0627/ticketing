using Microsoft.Extensions.Options;

namespace Ticketing.Infrastructure.Security;

/// <summary>
/// 綁 <c>Google:*</c> 設定。
///
/// <c>ClientId</c> 是**公開值**：它會出現在前端原始碼裡，所以放 appsettings.json、可以進 git。
/// 我們沒有 client secret——前端拿 ID token、後端只驗證它（設計文件 05 第 2 節）。
/// </summary>
public sealed class GoogleAuthOptions
{
    public const string SectionName = "Google";

    /// <summary>關掉時 <c>/auth/google</c> 回 401，而不是讓整個 API 起不來。</summary>
    public bool Enabled { get; set; } = true;

    public string ClientId { get; set; } = "";
}

public sealed class GoogleAuthOptionsValidator : IValidateOptions<GoogleAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, GoogleAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled) return ValidateOptionsResult.Success;

        return string.IsNullOrWhiteSpace(options.ClientId)
            ? ValidateOptionsResult.Fail("Google:Enabled 是 true 時必須提供 Google:ClientId。")
            : ValidateOptionsResult.Success;
    }
}
