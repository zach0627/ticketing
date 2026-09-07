using Ticketing.Domain.Booking;
using Ticketing.Domain.Common;
using Ticketing.Domain.Orders;

namespace Ticketing.Domain.Tests;

public class OrderTests
{
    private static readonly Guid OrderId = new("0f8fad5b-d9cb-469f-a165-70867728950e");

    [Fact]
    public void Copies_the_hold_snapshot_rather_than_recomputing_prices()
    {
        var hold = TestData.Hold(seatCount: 2);
        var performance = TestData.Performance();

        var order = Order.FromHold(OrderId, hold, performance, TestData.Now);

        Assert.Equal(hold.Id, order.HoldId);
        Assert.Equal(hold.BuyerId, order.BuyerId);
        Assert.Equal(hold.PerformanceId, order.PerformanceId);
        Assert.Equal(hold.TotalAmount, order.TotalAmount);
        Assert.Equal("TWD", order.Currency);
        Assert.Equal(performance.Event.Title, order.EventTitleSnapshot);
        Assert.Equal(performance.StartsAtUtc, order.StartsAtSnapshot);
        Assert.Equal(TestData.Now, order.CreatedAtUtc);
        Assert.Equal(2, order.Items.Count);
    }

    [Fact]
    public void Ticket_codes_are_order_id_plus_a_two_digit_ordinal_by_seat_id()
    {
        var hold = TestData.Hold(seatCount: 3);

        var order = Order.FromHold(OrderId, hold, TestData.Performance(), TestData.Now);

        var bySeat = order.Items.OrderBy(i => i.SeatId).ToList();
        Assert.Equal($"{OrderId:N}-01", bySeat[0].TicketCode);
        Assert.Equal($"{OrderId:N}-02", bySeat[1].TicketCode);
        Assert.Equal($"{OrderId:N}-03", bySeat[2].TicketCode);
    }

    [Fact]
    public void Ticket_codes_fit_the_forty_character_column()
    {
        var order = Order.FromHold(OrderId, TestData.Hold(), TestData.Performance(), TestData.Now);

        Assert.All(order.Items, i => Assert.Equal(35, i.TicketCode.Length));
    }

    [Fact]
    public void Ticket_codes_are_unique_within_an_order()
    {
        var order = Order.FromHold(OrderId, TestData.Hold(seatCount: 4), TestData.Performance(), TestData.Now);

        Assert.Equal(4, order.Items.Select(i => i.TicketCode).Distinct().Count());
    }

    [Fact]
    public void Item_addresses_and_prices_come_from_the_hold_items()
    {
        var hold = TestData.Hold(seatCount: 2);

        var order = Order.FromHold(OrderId, hold, TestData.Performance(), TestData.Now);

        foreach (var holdItem in hold.Items)
        {
            var orderItem = order.Items.Single(i => i.SeatId == holdItem.SeatId);
            Assert.Equal(holdItem.SectionCode, orderItem.SectionCode);
            Assert.Equal(holdItem.RowNumber, orderItem.RowNumber);
            Assert.Equal(holdItem.SeatNumber, orderItem.SeatNumber);
            Assert.Equal(holdItem.UnitPrice, orderItem.UnitPrice);
        }
    }

    [Fact]
    public void A_hold_from_another_performance_is_rejected()
    {
        var hold = TestData.Hold();
        var otherPerformance = new Ticketing.Domain.Catalog.Performance(
            2, TestData.Event(), "別的場館", "台中",
            TestData.Now.AddDays(30), TestData.Now.AddDays(-1), TestData.Now.AddDays(29), 120);

        var ex = Assert.Throws<BookingRuleException>(() =>
            Order.FromHold(OrderId, hold, otherPerformance, TestData.Now));

        Assert.Equal(ErrorCode.ValidationFailed, ex.Code);
    }

    [Fact]
    public void Order_totals_equal_the_sum_of_its_items()
    {
        var order = Order.FromHold(OrderId, TestData.Hold(seatCount: 3), TestData.Performance(), TestData.Now);

        Assert.Equal(order.Items.Sum(i => i.UnitPrice), order.TotalAmount);
    }

    [Fact]
    public void Items_cannot_be_mutated_through_the_public_surface()
    {
        var order = Order.FromHold(OrderId, TestData.Hold(), TestData.Performance(), TestData.Now);

        Assert.IsNotType<List<OrderItem>>(order.Items);
    }
}
