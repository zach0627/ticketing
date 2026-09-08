using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.Extensions.Options;

namespace Ticketing.Infrastructure.Security;

/// <summary>
/// 綁 <c>Jwt:*</c> 設定。與 <c>SeedOptions</c> 相反，這組**一定要 ValidateOnStart**：
/// 少了簽章金鑰的 API 不該啟動成功再在第一次登入時才爆掉
/// ——啟動就失敗，錯誤訊息才會出現在部署 log 的最上面（設計文件 05 第 6 節）。
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required, StringLength(64, MinimumLength = 1)]
    public string Issuer { get; set; } = "";

    [Required, StringLength(64, MinimumLength = 1)]
    public string Audience { get; set; } = "";

    /// <summary>HMAC 對稱金鑰。**只從 user-secrets 或 App Service 設定來，永遠不進 git。**</summary>
    [Required]
    public string SigningKey { get; set; } = "";

    /// <summary>存取權杖有效期。本專案沒有 refresh token，固定 120 分鐘（設計文件 05 第 9 節）。</summary>
    [Range(120, 120)]
    public int AccessTokenMinutes { get; set; } = 120;
}

/// <summary>
/// DataAnnotations 管得到「有沒有填」，管不到「填得夠不夠格」。
///
/// 兩條規則都是實務上真的踩過的坑：
/// HS256 的金鑰短於 32 bytes 會被 <c>SymmetricSecurityKey</c> 直接拒絕；
/// 而範本裡的佔位字串被原封不動帶上正式環境，是公開 repo 最常見的外洩方式。
/// </summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private const int MinimumKeyBytes = 32;   // HMAC-SHA256 的最小金鑰長度

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        // 長度不等於熵：這只擋得住「太短」，不保證金鑰是安全隨機來源產生的。
        // 產生方式寫在設計文件 20，不在程式裡強制。
        var keyBytes = Encoding.UTF8.GetByteCount(options.SigningKey);
        if (keyBytes < MinimumKeyBytes)
            failures.Add($"Jwt:SigningKey 至少要 {MinimumKeyBytes} bytes（UTF-8），目前只有 {keyBytes}。");

        if (LooksLikePlaceholder(options.SigningKey))
            failures.Add("Jwt:SigningKey 還是文件裡的佔位字串，請改成自己產生的隨機金鑰。");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool LooksLikePlaceholder(string value)
        => value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
        || value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
        || value.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);
}
