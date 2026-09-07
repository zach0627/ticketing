using Ticketing.Domain.Booking;
using Ticketing.Domain.Common;

namespace Ticketing.Domain.Tests;

public class SeatHoldItemTests
{
    [Fact]
    public void Snapshots_the_address_and_price_at_hold_time()
    {
        var section = TestData.Section(price: 3200m);
        var seat = TestData.Seat(1103, row: 2, number: 7, sectionId: section.Id);

        var item = new SeatHoldItem(seat, section);

        Assert.Equal(1103, item.SeatId);
        Assert.Equal("A", item.SectionCode);
        Assert.Equal(2, item.RowNumber);
        Assert.Equal(7, item.SeatNumber);
        Assert.Equal(3200m, item.UnitPrice);
    }

    [Fact]
    public void A_seat_from_another_section_is_rejected()
    {
        var section = TestData.Section(id: 11);
        var foreignSeat = TestData.Seat(1201, 1, 1, sectionId: 12);

        var ex = Assert.Throws<BookingRuleException>(() => new SeatHoldItem(foreignSeat, section));

        Assert.Equal(ErrorCode.ValidationFailed, ex.Code);
    }
}
