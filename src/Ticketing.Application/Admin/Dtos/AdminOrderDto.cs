namespace Ticketing.Application.Admin.Dtos;

/// <summary>
/// 後台訂單列表。比使用者的 <c>OrderSummaryDto</c> 多兩個欄位。
/// **只給顯示名，不給 Email**——管理者要看的是「誰買的」，不需要聯絡方式（設計文件 13 第 2.1 節）。
/// </summary>
public sealed record AdminOrderDto(
    Guid Id,
    Guid HoldId,
    int PerformanceId,
    string EventTitle,
    DateTimeOffset StartsAtUtc,
    string BuyerDisplayName,
    int Quantity,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAtUtc);
