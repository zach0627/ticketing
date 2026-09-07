using Ticketing.Domain.Common;

namespace Ticketing.Domain.Tests;

public class SectionTests
{
    [Fact]
    public void Capacity_is_rows_times_seats_per_row()
        => Assert.Equal(40, TestData.Section(rows: 4, seatsPerRow: 10).Capacity);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_price_is_rejected(decimal price)
    {
        var ex = Assert.Throws<BookingRuleException>(() => TestData.Section(price: price));
        Assert.Equal(ErrorCode.ValidationFailed, ex.Code);
    }

    [Fact]
    public void Non_positive_geometry_is_rejected()
        => Assert.Throws<BookingRuleException>(() => TestData.Section(rows: 0));
}
