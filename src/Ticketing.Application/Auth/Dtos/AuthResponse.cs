namespace Ticketing.Application.Auth.Dtos;

/// <summary>
/// 三條登入路徑的共同回應。<c>AccessToken</c> 是 JWT，前端放 sessionStorage。
/// </summary>
public sealed record AuthResponse(string AccessToken, UserDto User);
