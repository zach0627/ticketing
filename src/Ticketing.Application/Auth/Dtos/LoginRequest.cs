using System.ComponentModel.DataAnnotations;

namespace Ticketing.Application.Auth.Dtos;

/// <summary>
/// 登入。密碼**不設下限**：帳號是舊規則建立的也要能登入，
/// 而且在登入端點檢查長度下限等於告訴攻擊者「這個密碼太短，不用試了」。
/// </summary>
public sealed record LoginRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = "";

    [Required, StringLength(128)]
    public string Password { get; init; } = "";
}
