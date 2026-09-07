using Ticketing.Domain.Catalog;
using Ticketing.Domain.Common;

namespace Ticketing.Domain.Tests;

public class SeatTests
{
    [Fact]
    public void A_new_seat_is_available()
    {
        var seat = TestData.Seat(1101, 1, 1);

        Assert.Equal(SeatStatus.Available, seat.Status);
        Assert.Null(seat.HoldId);
        Assert.Null(seat.HeldUntilUtc);
        Assert.True(seat.IsAvailableAt(TestData.Now));
    }

    [Fact]
    public void A_seat_held_into_the_future_is_not_available()
    {
        var seat = TestData.HeldSeat(1101, 1, 1, heldUntil: TestData.Now.AddMinutes(5));

        Assert.False(seat.IsAvailableAt(TestData.Now));
    }

    [Fact]
    public void A_seat_whose_hold_has_expired_counts_as_available()
    {
        // 沒有背景清理程式：過期的 Held 直接視同可用（設計文件 04 第 5 節）
        var seat = TestData.HeldSeat(1101, 1, 1, heldUntil: TestData.Now.AddMinutes(-1));

        Assert.True(seat.IsAvailableAt(TestData.Now));
    }

    [Fact]
    public void Exactly_at_the_hold_expiry_instant_the_seat_is_available_again()
    {
        var seat = TestData.HeldSeat(1101, 1, 1, heldUntil: TestData.Now);

        Assert.True(seat.IsAvailableAt(TestData.Now));
    }

    [Fact]
    public void A_sold_seat_is_never_available()
    {
        var seat = TestData.SoldSeat(1101, 1, 1);

        Assert.False(seat.IsAvailableAt(TestData.Now));
        Assert.False(seat.IsAvailableAt(TestData.Now.AddYears(1)));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1101, 0, 1)]
    [InlineData(1101, 1, 0)]
    public void Invalid_identifiers_are_rejected(int id, int row, int number)
        => Assert.Throws<BookingRuleException>(() => new Seat(id == 0 ? 0 : id, 1, 11, row, number));
}
