using Ticketing.Domain.Booking;
using Ticketing.Domain.Catalog;

namespace Ticketing.Domain.Tests;

public class ContiguousSeatFinderTests
{
    private static List<Seat> Row(int row, params int[] numbers)
        => numbers.Select(n => TestData.Seat(row * 100 + n, row, n)).ToList();

    [Fact]
    public void Finds_the_first_run_in_the_lowest_row()
    {
        var available = Row(1, 1, 2, 3, 4, 5).Concat(Row(2, 1, 2, 3)).ToList();

        var found = ContiguousSeatFinder.Find(available, 3);

        Assert.NotNull(found);
        Assert.Equal([1, 2, 3], found.Select(s => s.SeatNumber));
        Assert.All(found, s => Assert.Equal(1, s.RowNumber));
    }

    [Fact]
    public void Skips_a_gap_and_uses_the_next_run_in_the_same_row()
    {
        // 3 號被別人拿走：1,2 不夠，答案是 4,5,6
        var available = Row(1, 1, 2, 4, 5, 6);

        var found = ContiguousSeatFinder.Find(available, 3);

        Assert.NotNull(found);
        Assert.Equal([4, 5, 6], found.Select(s => s.SeatNumber));
    }

    [Fact]
    public void Returns_null_when_no_single_row_has_enough_contiguous_seats()
    {
        // 兩排各有 2 席相連，但不跨排、不拆單
        var available = Row(1, 1, 2).Concat(Row(2, 5, 6)).ToList();

        Assert.Null(ContiguousSeatFinder.Find(available, 3));
    }

    [Fact]
    public void Input_order_does_not_matter()
    {
        var shuffled = new List<Seat>
        {
            TestData.Seat(105, 1, 5), TestData.Seat(102, 1, 2),
            TestData.Seat(104, 1, 4), TestData.Seat(103, 1, 3)
        };

        var found = ContiguousSeatFinder.Find(shuffled, 3);

        Assert.NotNull(found);
        Assert.Equal([2, 3, 4], found.Select(s => s.SeatNumber));
    }

    [Fact]
    public void Prefers_the_lower_row_even_when_a_later_row_has_a_longer_run()
    {
        var available = Row(2, 1, 2, 3, 4, 5, 6, 7, 8).Concat(Row(1, 1, 2)).ToList();

        var found = ContiguousSeatFinder.Find(available, 2);

        Assert.NotNull(found);
        Assert.All(found, s => Assert.Equal(1, s.RowNumber));   // 找第一段就回，不挑最好的
    }

    [Fact]
    public void Empty_input_returns_null() => Assert.Null(ContiguousSeatFinder.Find([], 2));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_quantity_returns_null(int quantity)
        => Assert.Null(ContiguousSeatFinder.Find(Row(1, 1, 2, 3), quantity));

    [Fact]
    public void A_run_of_exactly_the_requested_size_is_accepted()
    {
        var found = ContiguousSeatFinder.Find(Row(1, 7, 8), 2);

        Assert.NotNull(found);
        Assert.Equal([7, 8], found.Select(s => s.SeatNumber));
    }
}
