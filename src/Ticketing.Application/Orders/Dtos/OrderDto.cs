namespace Ticketing.Application.Orders.Dtos;

/// <summary>
/// 一張訂單。活動名稱與時間是**下單當時的快照**，
/// 之後活動改名或改期都不影響已成立的訂單（設計文件 04 第 8 節）。
/// 不含 <c>buyerId</c>：那是查詢條件，不是回應內容。
/// </summary>
public sealed record OrderDto(
    Guid Id,
    Guid HoldId,
    string EventTitle,
    DateTimeOffset StartsAtUtc,
    string Currency,
    decimal TotalAmount,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<OrderItemDto> Items);
