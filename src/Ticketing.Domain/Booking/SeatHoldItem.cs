using Ticketing.Domain.Catalog;
using Ticketing.Domain.Common;

namespace Ticketing.Domain.Booking;

/// <summary>
/// 保留裡的一席。區／排／號／單價是**保留當下的快照**（ADR-8）：
/// 這樣 SeatHold → HoldDto 能直接對應，Order.FromHold 也不必再載入座位。
/// </summary>
public sealed class SeatHoldItem
{
    public int SeatId { get; private set; }
    public string SectionCode { get; private set; } = "";
    public int RowNumber { get; private set; }
    public int SeatNumber { get; private set; }
    public decimal UnitPrice { get; private set; }

    private SeatHoldItem() { }                              // EF Core 用

    public SeatHoldItem(Seat seat, Section section)
    {
        ArgumentNullException.ThrowIfNull(seat);
        ArgumentNullException.ThrowIfNull(section);

        if (seat.SectionId != section.Id)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "座位不屬於此票區");

        SeatId = seat.Id;
        SectionCode = section.Code;
        RowNumber = seat.RowNumber;
        SeatNumber = seat.SeatNumber;
        UnitPrice = section.Price;
    }
}
