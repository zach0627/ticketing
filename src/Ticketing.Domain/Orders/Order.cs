using Ticketing.Domain.Booking;
using Ticketing.Domain.Catalog;
using Ticketing.Domain.Common;

namespace Ticketing.Domain.Orders;

/// <summary>
/// 模擬付款成功的結果，一張票一列。
/// 所有金額與座位資料都是**從保留複製過來的快照**，活動之後改名改價都不影響已成立的訂單。
/// </summary>
public sealed class Order
{
    public Guid Id { get; private set; }
    public Guid HoldId { get; private set; }
    public Guid BuyerId { get; private set; }
    public int PerformanceId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "TWD";
    public string EventTitleSnapshot { get; private set; } = "";
    public DateTimeOffset StartsAtSnapshot { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private readonly List<OrderItem> _items = [];
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    private Order() { }                                     // EF Core 用

    /// <summary>
    /// 由保留產生訂單。呼叫端必須已經完成交易協定（兩道 gate、條件 UPDATE 成功）；
    /// 這裡只保證**這張訂單自己**的資料一致：同場、席位不重複、總額等於明細加總。
    /// 票號規則：<c>{orderId:N}-{序號:D2}</c>，序號按 SeatId 升序從 1 起。
    /// </summary>
    public static Order FromHold(Guid orderId, SeatHold hold, Performance performance, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(hold);
        ArgumentNullException.ThrowIfNull(performance);

        if (hold.PerformanceId != performance.Id)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "保留與場次不一致");
        if (hold.Items.Count == 0)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "保留沒有任何座位");
        if (hold.Items.Select(i => i.SeatId).Distinct().Count() != hold.Items.Count)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "保留有重複座位");

        var itemsTotal = hold.Items.Sum(i => i.UnitPrice);
        if (itemsTotal != hold.TotalAmount)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "保留總額與明細不一致");

        var order = new Order
        {
            Id = orderId,
            HoldId = hold.Id,
            BuyerId = hold.BuyerId,
            PerformanceId = hold.PerformanceId,
            TotalAmount = hold.TotalAmount,
            Currency = "TWD",
            EventTitleSnapshot = performance.Event.Title,
            StartsAtSnapshot = performance.StartsAtUtc,
            CreatedAtUtc = now
        };

        var ordinal = 1;
        foreach (var source in hold.Items.OrderBy(i => i.SeatId))
            order._items.Add(new OrderItem(orderId, source, ordinal++));

        return order;
    }
}
