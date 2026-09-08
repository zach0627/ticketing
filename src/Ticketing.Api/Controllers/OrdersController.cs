using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Api.Auth;
using Ticketing.Application.Common;
using Ticketing.Application.Orders;
using Ticketing.Application.Orders.Dtos;
using Ticketing.Domain.Common;

namespace Ticketing.Api.Controllers;

/// <summary>
/// 訂單查詢。走 <see cref="IOrderQueries"/> 直接投影成 DTO，不經過 Domain（ADR-5）——
/// 這裡沒有任何規則要執行，只是把資料讀出來。
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class OrdersController(IOrderQueries queries, CurrentUser currentUser) : ControllerBase
{
    [HttpGet("orders")]
    [ProducesResponseType<PagedResult<OrderSummaryDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<OrderSummaryDto>> GetMine(
        [FromQuery, Range(1, 1000)] int page = 1,
        [FromQuery, Range(1, 30)] int pageSize = 10,
        CancellationToken ct = default)
        => queries.GetMyOrdersAsync(currentUser.Id, page, pageSize, ct);

    /// <summary>買家 id 在 WHERE 裡，所以別人的訂單「查不到」而不是「不給看」。</summary>
    [HttpGet("orders/{id:guid}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken ct)
        => await queries.GetMyOrderAsync(currentUser.Id, id, ct)
           ?? throw new BookingRuleException(ErrorCode.NotFound, "找不到這張訂單");
}
