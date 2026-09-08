using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ticketing.Api.Auth;
using Ticketing.Api.RateLimiting;
using Ticketing.Application.Admin;
using Ticketing.Application.Admin.Dtos;
using Ticketing.Application.Common;
using Ticketing.Domain.Users;

namespace Ticketing.Api.Controllers;

/// <summary>
/// 管理後台。
///
/// <c>[Authorize(Roles = "Admin")]</c> 掛在**類別**上：新增一個動作時預設就是受保護的，
/// 忘記加 attribute 不會變成漏洞。前端藏按鈕只是 UX，真正的把關在這裡
/// （設計文件 14 第 1 節）。
///
/// 兩個寫入操作都要 <c>Idempotency-Key</c>，而且去重紀錄存在**不會被重置刪除的**
/// `AdminAudits`——存在 `IdempotencyRecords` 的話，重置會把自己的去重證據一起刪掉。
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminController(IAdminService admin, IAdminQueries queries,
                                    CurrentUser currentUser, TimeProvider clock) : ControllerBase
{
    [HttpGet("dashboard")]
    [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK)]
    public Task<DashboardDto> Dashboard(CancellationToken ct)
        => queries.GetDashboardAsync(clock.GetUtcNow(), ct);

    [HttpGet("orders")]
    [ProducesResponseType<PagedResult<AdminOrderDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<AdminOrderDto>> Orders(
        [FromQuery] int? performanceId,
        [FromQuery, Range(1, 1000)] int page = 1,
        [FromQuery, Range(1, 30)] int pageSize = 10,
        CancellationToken ct = default)
        => queries.GetOrdersAsync(performanceId, page, pageSize, ct);

    /// <summary>暫停或恢復單一場次的售票。只擋**新的**保留，既有的仍可付款。</summary>
    [HttpPatch("performances/{performanceId:int}")]
    [EnableRateLimiting(RateLimitPolicies.AdminWrite)]
    [ProducesResponseType<PauseSalesResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SetSalesPaused(
        int performanceId,
        PauseSalesRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string idempotencyKey,
        CancellationToken ct)
        => Send(await admin.SetSalesPausedAsync(currentUser.Id, performanceId, request,
                                                Guid.Parse(IdempotencyKey.Canonical(idempotencyKey)), ct));

    /// <summary>
    /// 一鍵重置：清掉所有購買資料、座位全部回可售、活動日期往後平移。
    /// <c>confirmation</c> 必須精確等於 <c>RESET</c>，由**模型驗證**把關。
    /// </summary>
    [HttpPost("reset")]
    [EnableRateLimiting(RateLimitPolicies.AdminWrite)]
    [ProducesResponseType<ResetResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Reset(
        ResetRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string idempotencyKey,
        CancellationToken ct)
        => Send(await admin.ResetAsync(currentUser.Id, request,
                                       Guid.Parse(IdempotencyKey.Canonical(idempotencyKey)), ct));

    /// <summary>管理操作成功一律 200，所以這裡不需要像購票那樣判斷狀態碼。</summary>
    private ActionResult Send(AdminOperationResponse response) => new ContentResult
    {
        StatusCode = StatusCodes.Status200OK,
        ContentType = "application/json",
        Content = response.ResponseJson
    };
}
