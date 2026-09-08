using Riok.Mapperly.Abstractions;
using Ticketing.Application.Orders.Dtos;
using Ticketing.Domain.Orders;

namespace Ticketing.Application.Orders;

/// <summary>
/// <see cref="Order"/> → <see cref="OrderDto"/>。
///
/// 兩個欄位要改名：Domain 用 <c>EventTitleSnapshot</c>／<c>StartsAtSnapshot</c> 強調
/// 「這是快照，不是即時值」，但那是**內部**的提醒，API 契約用的是
/// <c>eventTitle</c>／<c>startsAtUtc</c>（設計文件 13 第 2 節）。
///
/// <c>BuyerId</c> 與 <c>PerformanceId</c> 明確忽略：買家是查詢條件不是回應內容，
/// 場次 id 對訂單頁沒有用途。這兩行不是樣板，是 RMG020 逼出來的**明確決定**。
/// </summary>
[Mapper]
public static partial class OrderMapper
{
    [MapProperty(nameof(Order.EventTitleSnapshot), nameof(OrderDto.EventTitle))]
    [MapProperty(nameof(Order.StartsAtSnapshot), nameof(OrderDto.StartsAtUtc))]
    [MapperIgnoreSource(nameof(Order.BuyerId))]
    [MapperIgnoreSource(nameof(Order.PerformanceId))]
    public static partial OrderDto ToDto(Order order);

    [MapperIgnoreSource(nameof(OrderItem.OrderId))]
    [MapperIgnoreSource(nameof(OrderItem.SeatId))]
    private static partial OrderItemDto ToItemDto(OrderItem item);
}
