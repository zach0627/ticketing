using System.ComponentModel.DataAnnotations;

namespace Ticketing.Application.Auth.Dtos;

/// <summary>
/// 前端用 Google Identity Services 拿到的 ID token（<c>credential</c>）。
/// 我們只驗證它，不代表使用者去換 access token，所以**不需要 client secret**
/// （設計文件 05 第 2 節）。
/// </summary>
public sealed record GoogleLoginRequest
{
    [Required, StringLength(8192)]
    public string IdToken { get; init; } = "";
}
