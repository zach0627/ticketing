using Ticketing.Application.Auth.Dtos;

namespace Ticketing.Application.Auth;

/// <summary>
/// 三條登入路徑與「我是誰」。
///
/// 簽章收的是 <c>Guid userId</c> 而不是 <c>ClaimsPrincipal</c>——
/// Application 從頭到尾不認識 <c>HttpContext</c>，取出 <c>sub</c> 是 Api 層的工作
/// （設計文件 05 第 4.3 節）。
/// </summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResponse> GoogleAsync(GoogleLoginRequest request, CancellationToken ct);
    Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct);
}
