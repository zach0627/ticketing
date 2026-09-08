using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ticketing.Api.Tests.Infrastructure;

/// <summary>
/// 只有測試看得到的探針端點。
///
/// 為什麼需要它？<c>[Authorize]</c> 的 401、<c>[Authorize(Roles)]</c> 的 403，
/// 以及它們有沒有走進我們的 <c>ApiProblemWriter</c>，是**階段 5 接的線**；
/// 但第一個真正需要授權的業務端點要到階段 6（保留）與階段 7（後台）才存在。
///
/// 與其等兩個階段之後才發現 <c>OnForbidden</c> 沒接好，不如現在就用兩個
/// 沒有任何業務邏輯的端點把接線釘住。它們編譯在**測試組件**裡，
/// 由 <see cref="TicketingApiFactory"/> 明確加入 ApplicationPart 才會被路由看到，
/// 正式發布的 API 沒有這兩條路徑。
/// </summary>
[ApiController]
[Route("api/v1/test-only")]
public sealed class AuthProbeController : ControllerBase
{
    [HttpGet("any-user")]
    [Authorize]
    public IActionResult AnyUser() => Ok(new { scope = "any-user" });

    [HttpGet("admin-only")]
    [Authorize(Roles = "Admin")]
    public IActionResult AdminOnly() => Ok(new { scope = "admin-only" });
}
