namespace Ticketing.Application.Booking;

/// <summary>
/// 冪等操作的結果：**已經決定好的 HTTP 狀態與要送出去的 JSON**。
///
/// 為什麼 Application 會回一個帶 HTTP 狀態的東西？因為「同一個 key 重送要拿到同樣的結果」
/// 這件事，結果本身就包含狀態碼（201 建立 vs 200 已存在 vs 402 模擬失敗）。
/// Controller 只負責把它送出去，不重新決定（設計文件 13 第 2 節）。
///
/// <paramref name="IsReplay"/> **不會**進到 <paramref name="ResponseJson"/> 裡——
/// 它是內部資訊（用來決定要不要再排一次通知），不是 API 契約的一部分。
/// </summary>
public sealed record IdempotencyResponse(int HttpStatus, string ResponseJson, bool IsReplay)
{
    /// <summary>
    /// 這一次真的建立了訂單時才有值。跟 <see cref="IsReplay"/> 一樣是**內部資訊**：
    /// 它不會出現在 <see cref="ResponseJson"/> 裡，只讓呼叫端知道 commit 之後要不要
    /// 記一筆成功 log、要不要排一則通知。
    /// </summary>
    public Guid? CreatedOrderId { get; init; }
}

/// <summary>
/// 存起來的失敗結果（目前只有模擬付款失敗）。
/// 重播時由 <c>ApiProblemWriter</c> 重新產生 ProblemDetails，
/// 並帶上**當次請求**的 traceId——所以業務結果相同，但錯誤 body 不會逐位元相同。
/// </summary>
public sealed record StoredFailure(string Code, string Message);
