using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ticketing.Api.Auth;
using Ticketing.Api.RateLimiting;
using Ticketing.Application.Booking;
using Ticketing.Application.Booking.Dtos;
using Ticketing.Application.Common;
using Ticketing.Application.Orders.Dtos;
using Ticketing.Domain.Common;

namespace Ticketing.Api.Controllers;

/// <summary>
/// 選位、保留、付款、取消。**全部需要登入**（類別層的 <c>[Authorize]</c>）。
///
/// 兩個寫入端點回的是 Application 已經決定好的 HTTP 狀態與 JSON——
/// Controller 不重新決定「這次算 201 還是 200」，因為那是冪等協定的一部分
/// （設計文件 13 第 2 節）。
///
/// 買家一律來自 <see cref="CurrentUser"/>，**不從 body 或 query 讀**。
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class BookingController(IBookingService booking, CurrentUser currentUser) : ControllerBase
{
    /// <summary>建立保留。需要 <c>Idempotency-Key</c> 標頭。</summary>
    [HttpPost("performances/{performanceId:int}/holds")]
    [EnableRateLimiting(RateLimitPolicies.BookingWrite)]
    [ProducesResponseType<HoldDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<HoldDto>> CreateHold(
        int performanceId,
        CreateHoldRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string idempotencyKey,
        CancellationToken ct)
        => Send(await booking.CreateHoldAsync(currentUser.Id, performanceId, request,
                                              IdempotencyKey.Canonical(idempotencyKey), ct));

    /// <summary>模擬付款。需要 <c>Idempotency-Key</c> 標頭。</summary>
    [HttpPost("holds/{id:guid}/checkout")]
    [EnableRateLimiting(RateLimitPolicies.BookingWrite)]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Checkout(
        Guid id,
        CheckoutRequest request,
        [FromHeader(Name = "Idempotency-Key"), Required] string idempotencyKey,
        CancellationToken ct)
        => Send(await booking.CheckoutAsync(currentUser.Id, id, request,
                                            IdempotencyKey.Canonical(idempotencyKey), ct));

    /// <summary>取消。**不需要 key**——它的資料效果本來就是冪等的，重送一樣回 200。</summary>
    [HttpPost("holds/{id:guid}/cancel")]
    [EnableRateLimiting(RateLimitPolicies.BookingWrite)]
    [ProducesResponseType<HoldDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<HoldDto> Cancel(Guid id, CancellationToken ct)
        => booking.CancelHoldAsync(currentUser.Id, id, ct);

    /// <summary>查一筆保留。別人的一律 404，不告訴你它存在。</summary>
    [HttpGet("holds/{id:guid}")]
    [ProducesResponseType<HoldDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<HoldDto> Get(Guid id, CancellationToken ct)
        => booking.GetHoldAsync(currentUser.Id, id, ct);

    /// <summary>本人在某場次還沒到期的保留（0 或 1 筆）。選位頁進來先問這個。</summary>
    [HttpGet("me/holds")]
    [ProducesResponseType<HoldListDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<HoldListDto> MyHolds([FromQuery, Range(1, int.MaxValue)] int performanceId,
                                     CancellationToken ct)
        => booking.GetActiveHoldsAsync(currentUser.Id, performanceId, ct);

    /// <summary>
    /// 把 Application 存下來的結果送出去。
    ///
    /// 402 走**丟例外**這條路是刻意的：存起來的只有 <c>{code, message}</c>，
    /// ProblemDetails 的其他欄位（尤其 <c>traceId</c>）必須是**當次請求**的值，
    /// 所以交給既有的 <c>ApiExceptionHandler</c> → <c>ApiProblemWriter</c> 重新產生，
    /// 而不是把第一次的錯誤 body 原封不動重播（設計文件 06 第 7 節）。
    /// </summary>
    private ActionResult Send(IdempotencyResponse response)
    {
        if (response.HttpStatus == StatusCodes.Status402PaymentRequired)
        {
            var failure = JsonSerializer.Deserialize<StoredFailure>(response.ResponseJson, PublicJson.Options)
                ?? new StoredFailure(ErrorCode.MockPaymentFailed, "模擬付款失敗");

            throw new BookingRuleException(failure.Code, failure.Message);
        }

        // 成功的 body 是存起來的那一串，逐位元原樣送出——重播才會拿到跟第一次一樣的東西
        return new ContentResult
        {
            StatusCode = response.HttpStatus,
            ContentType = "application/json",
            Content = response.ResponseJson
        };
    }
}
