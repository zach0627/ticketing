using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ticketing.Api.Auth;
using Ticketing.Api.RateLimiting;
using Ticketing.Application.Auth;
using Ticketing.Application.Auth.Dtos;

namespace Ticketing.Api.Controllers;

/// <summary>
/// 註冊、登入與「我是誰」。
///
/// 跟 <see cref="CatalogController"/> 一樣薄：綁模型、呼叫 Service、回狀態碼。
/// 三條登入路徑的差異（要不要查 Google、要不要建帳號）全部在 Application，
/// 這裡看不出來——這正是分層想要的結果。
///
/// 限流只掛在三個 <c>auth/*</c> 動作上；<c>/me</c> 是登入後的一般查詢，
/// 不該跟登入共用「每分鐘 10 次」這種為了擋暴力破解而設的額度。
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class AuthController(IAuthService auth, CurrentUser currentUser) : ControllerBase
{
    /// <summary>Email 註冊。</summary>
    [HttpPost("auth/register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await auth.RegisterAsync(request, ct));

    /// <summary>Email 登入。帳號不存在與密碼錯誤回一樣的 401。</summary>
    [HttpPost("auth/login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct)
        => auth.LoginAsync(request, ct);

    /// <summary>Google 登入。前端送的是 Google Identity Services 給的 ID token。</summary>
    [HttpPost("auth/google")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<AuthResponse> Google(GoogleLoginRequest request, CancellationToken ct)
        => auth.GoogleAsync(request, ct);

    /// <summary>目前登入者。前端用它確認 token 還有效、並取得角色。</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<UserDto> Me(CancellationToken ct) => auth.GetMeAsync(currentUser.Id, ct);
}
