using System.ComponentModel.DataAnnotations;

namespace Ticketing.Application.Auth.Dtos;

/// <summary>
/// Email 註冊。長度限制與 API 的 16 KiB body 上限一起生效，
/// 避免有人送巨量輸入逼我們去跑昂貴的密碼雜湊（設計文件 05 第 4.1 節）。
///
/// 密碼**不 trim、不做複雜度規則**：使用者刻意放的空白是密碼的一部分，
/// 複雜度規則則是安全劇場，長度才是有效的門檻。
/// </summary>
public sealed record RegisterRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = "";

    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; init; } = "";

    [Required, StringLength(80)]
    public string DisplayName { get; init; } = "";
}
