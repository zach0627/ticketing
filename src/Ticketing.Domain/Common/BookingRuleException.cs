namespace Ticketing.Domain.Common;

/// <summary>
/// 業務規則不成立。<see cref="Code"/> 取自 <see cref="ErrorCode"/>，
/// 由 Api 的 ApiExceptionHandler 轉成 ProblemDetails（設計文件 04 第 2.5 節）。
/// 這個型別不表示 HTTP 狀態，也不用來包裝資料庫或程式錯誤——
/// 未預期的例外必須維持原樣往上拋，不能冒充成業務衝突。
/// </summary>
public sealed class BookingRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
