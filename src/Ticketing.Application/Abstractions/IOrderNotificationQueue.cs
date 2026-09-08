namespace Ticketing.Application.Abstractions;

/// <summary>
/// 訂單成立後的通知佇列。
///
/// 回 <c>bool</c> 而不是 <c>Task</c> 是刻意的：**排隊這件事不可以等**。
/// 它在交易 commit **之後**被呼叫，滿了就回 false，呼叫端記一筆 warning 就好——
/// 沒有任何票務狀態依賴通知成功（設計文件 06 第 9 節）。
///
/// 需要「保證送達」時才換成 Outbox（設計文件 17），那是另一個量級的東西。
/// </summary>
public interface IOrderNotificationQueue
{
    bool TryEnqueue(Guid orderId);
}
