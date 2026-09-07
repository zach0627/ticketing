using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Domain.Common;

namespace Ticketing.Api.ExceptionHandling;

/// <summary>
/// **所有**受 API 控制的錯誤都經過這裡，格式才會一致（設計文件 13 第 3 節）。
///
/// <c>AddProblemDetails()</c> 只註冊服務，**不會**自動替模型驗證 400、challenge 401、
/// forbid 403、限流 429 補上我們的 <c>code</c>——那些入口要各自接上這個 writer。
///
/// 無狀態，註冊為 Singleton；<c>HttpContext</c> 由參數傳入，不注入 accessor。
/// </summary>
public sealed class ApiProblemWriter
{
    /// <summary>錯誤碼 → HTTP 狀態與標題。沒列在這裡的一律 500，不冒充業務衝突。</summary>
    private static readonly Dictionary<string, (int Status, string Title)> Map = new(StringComparer.Ordinal)
    {
        [ErrorCode.ValidationFailed] = (StatusCodes.Status400BadRequest, "請求內容不正確"),
        [ErrorCode.Unauthorized] = (StatusCodes.Status401Unauthorized, "請先登入"),
        [ErrorCode.MockPaymentFailed] = (StatusCodes.Status402PaymentRequired, "模擬付款失敗"),
        [ErrorCode.Forbidden] = (StatusCodes.Status403Forbidden, "沒有權限"),
        [ErrorCode.NotFound] = (StatusCodes.Status404NotFound, "找不到資源"),
        [ErrorCode.MethodNotAllowed] = (StatusCodes.Status405MethodNotAllowed, "不支援的方法"),
        [ErrorCode.SeatUnavailable] = (StatusCodes.Status409Conflict, "座位已被取走"),
        [ErrorCode.NoContiguousSeats] = (StatusCodes.Status409Conflict, "沒有足夠的連續座位"),
        [ErrorCode.LimitExceeded] = (StatusCodes.Status409Conflict, "超過每人購票上限"),
        [ErrorCode.ActiveHoldExists] = (StatusCodes.Status409Conflict, "已有有效的保留"),
        [ErrorCode.HoldExpired] = (StatusCodes.Status409Conflict, "保留已到期"),
        [ErrorCode.HoldNotActive] = (StatusCodes.Status409Conflict, "保留不是有效狀態"),
        [ErrorCode.NotOnSale] = (StatusCodes.Status409Conflict, "目前不在售票期間"),
        [ErrorCode.IdempotencyKeyReuse] = (StatusCodes.Status409Conflict, "相同的 Idempotency-Key 用在不同內容"),
        [ErrorCode.EmailAlreadyRegistered] = (StatusCodes.Status409Conflict, "此 Email 已註冊"),
        [ErrorCode.PayloadTooLarge] = (StatusCodes.Status413PayloadTooLarge, "內容過大"),
        [ErrorCode.UnsupportedMediaType] = (StatusCodes.Status415UnsupportedMediaType, "不支援的內容型別"),
        [ErrorCode.RateLimited] = (StatusCodes.Status429TooManyRequests, "請求過於頻繁"),
        [ErrorCode.ServiceUnavailable] = (StatusCodes.Status503ServiceUnavailable, "服務暫時無法使用"),
        [ErrorCode.InternalError] = (StatusCodes.Status500InternalServerError, "伺服器發生未預期的錯誤")
    };

    public ProblemDetails Create(HttpContext context, string code, string? detail = null,
                                 IDictionary<string, object?>? extensions = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (status, title) = Map.TryGetValue(code, out var mapped)
            ? mapped
            : Map[ErrorCode.InternalError];

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        problem.Extensions["code"] = code;

        // traceId 與業務 log 用同一個值，才能從回應查回那次請求（設計文件 10 第 3 節）
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        // 只複製白名單 extensions；不外露 SQL、堆疊或任意 Exception.Data
        if (extensions is not null)
            foreach (var (key, value) in extensions)
                problem.Extensions[key] = value;

        return problem;
    }

    public async Task WriteAsync(HttpContext context, string code, string? detail = null,
                                 IDictionary<string, object?>? extensions = null)
    {
        var problem = Create(context, code, detail, extensions);

        if (context.Response.HasStarted) return;   // 已經寫過 body 就不覆寫

        context.Response.StatusCode = problem.Status!.Value;

        // contentType 必須傳給 WriteAsJsonAsync——先設 Response.ContentType 會被它覆寫成
        // application/json。RFC 7807 要求用 application/problem+json，客戶端才分得出
        // 這是結構化錯誤而不是一般 JSON 回應。
        await context.Response.WriteAsJsonAsync(
            problem, options: null, contentType: "application/problem+json", context.RequestAborted);
    }
}
