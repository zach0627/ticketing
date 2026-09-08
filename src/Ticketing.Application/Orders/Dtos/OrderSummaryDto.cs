namespace Ticketing.Application.Orders.Dtos;

/// <summary>
/// 訂單列表用。**不含明細**——列表頁不需要每一張票，
/// 少查那些欄位就是少一次 join（設計文件 13 第 2.1 節）。
/// </summary>
public sealed record OrderSummaryDto(
    Guid Id,
    Guid HoldId,
    string EventTitle,
    DateTimeOffset StartsAtUtc,
    int Quantity,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAtUtc);
