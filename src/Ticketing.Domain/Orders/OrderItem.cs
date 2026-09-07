using Ticketing.Domain.Booking;
using Ticketing.Domain.Common;

namespace Ticketing.Domain.Orders;

/// <summary>
/// 一張票。區／排／號／單價沿用保留當下的快照，**不重算即時價**（設計文件 04 第 8 節）。
/// </summary>
public sealed class OrderItem
{
    public Guid OrderId { get; private set; }
    public int SeatId { get; private set; }
    public string SectionCode { get; private set; } = "";
    public int RowNumber { get; private set; }
    public int SeatNumber { get; private set; }
    public decimal UnitPrice { get; private set; }

    /// <summary>可讀識別碼，**不是入場憑證，也不是安全秘密**。格式見 <see cref="Order.FromHold"/>。</summary>
    public string TicketCode { get; private set; } = "";

    private OrderItem() { }                                 // EF Core 用

    internal OrderItem(Guid orderId, SeatHoldItem source, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (ordinal <= 0)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "票號序號必須大於 0");

        OrderId = orderId;
        SeatId = source.SeatId;
        SectionCode = source.SectionCode;
        RowNumber = source.RowNumber;
        SeatNumber = source.SeatNumber;
        UnitPrice = source.UnitPrice;
        TicketCode = $"{orderId:N}-{ordinal:D2}";
    }
}
