using Microsoft.AspNetCore.Diagnostics;
using Ticketing.Domain.Common;

namespace Ticketing.Api.ExceptionHandling;

/// <summary>
/// 最外層的例外處理。業務例外查對照表；**未預期的例外一律 500 一般訊息**——
/// 絕不把資料庫或程式錯誤冒充成業務衝突（設計文件 03 第 5 節）。
/// </summary>
public sealed class ApiExceptionHandler(ApiProblemWriter writer, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is BookingRuleException rule)
        {
            // 業務規則不成立是預期內的事，用 Information 記錄就好，不是系統錯誤
            logger.LogInformation("BusinessRuleRejected {Code} {Path}", rule.Code, context.Request.Path);

            // Extensions 是 Domain／Application 明確放進來的白名單欄位（例如 holdId），
            // 不是把整個例外攤開——writer 也只複製它拿到的這些鍵
            await writer.WriteAsync(context, rule.Code, rule.Message,
                                    rule.Extensions?.ToDictionary(e => e.Key, e => e.Value));
            return true;
        }

        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            return false;   // 客戶端自己斷線，不必回應也不必記成錯誤

        logger.LogError(exception, "UnhandledException {Path}", context.Request.Path);
        await writer.WriteAsync(context, ErrorCode.InternalError);
        return true;
    }
}
