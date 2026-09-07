using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Application.Catalog;
using Ticketing.Application.Catalog.Dtos;
using Ticketing.Application.Common;
using Ticketing.Domain.Catalog;
using Ticketing.Domain.Common;

namespace Ticketing.Api.Controllers;

/// <summary>
/// 公開瀏覽：不需要登入。
///
/// Controller 只做 HTTP 翻譯——綁參數、呼叫查詢、回狀態碼。
/// **不開交易、不碰 DbContext、不寫規則**（設計文件 12 第 3 節）。
/// 時間由 <see cref="TimeProvider"/> 取一次往下傳，Controller 自己不讀系統時鐘。
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class CatalogController(ICatalogQueries queries, TimeProvider clock) : ControllerBase
{
    /// <summary>活動列表。</summary>
    [HttpGet("events")]
    [ProducesResponseType<PagedResult<EventCardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EventCardDto>>> GetEvents(
        [FromQuery] EventCategory? category,
        [FromQuery, Range(1, 1000)] int page = 1,
        [FromQuery, Range(1, 30)] int pageSize = 15,      // 上限 30，超過由模型驗證回 400
        CancellationToken ct = default)
        => await queries.GetEventCardsAsync(category, page, pageSize, clock.GetUtcNow(), ct);

    /// <summary>活動詳情。用 Code（C01…S03）而不是 Id，網址比較好讀也比較穩定。</summary>
    [HttpGet("events/{code}")]
    [ProducesResponseType<EventDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailDto>> GetEvent(
        [FromRoute, StringLength(3, MinimumLength = 3)] string code, CancellationToken ct)
        => await queries.GetEventDetailAsync(code, clock.GetUtcNow(), ct)
           ?? throw new BookingRuleException(ErrorCode.NotFound, "找不到這個活動");

    /// <summary>
    /// 座位圖。回傳的座位**不含 HoldId 或買家**；過期的 Held 已經在投影時算成 Available。
    /// </summary>
    [HttpGet("performances/{performanceId:int}/seats")]
    [ProducesResponseType<SeatMapDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeatMapDto>> GetSeatMap(int performanceId, CancellationToken ct)
        => await queries.GetSeatMapAsync(performanceId, clock.GetUtcNow(), ct)
           ?? throw new BookingRuleException(ErrorCode.NotFound, "找不到這個場次");
}
