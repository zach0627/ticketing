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

    /// <summary>
    /// 要一起回給前端的額外欄位，例如 <c>ActiveHoldExists</c> 要附上既有的 <c>holdId</c>，
    /// 前端才知道該把人導去哪一筆保留（設計文件 13 第 3 節）。
    ///
    /// **這是白名單**：只放前端需要的業務識別碼，
    /// 絕不放 SQL、堆疊或任意 <c>Exception.Data</c>。
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Extensions { get; init; }
}
