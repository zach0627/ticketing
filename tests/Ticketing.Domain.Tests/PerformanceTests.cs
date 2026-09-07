using Ticketing.Domain.Catalog;
using Ticketing.Domain.Common;

namespace Ticketing.Domain.Tests;

public class PerformanceTests
{
    private static readonly DateTimeOffset Opens = TestData.Now.AddDays(-1);
    private static readonly DateTimeOffset Closes = TestData.Now.AddDays(29);
    private static readonly DateTimeOffset Starts = TestData.Now.AddDays(30);

    private static Performance Subject() => TestData.Performance(opensAt: Opens, closesAt: Closes, startsAt: Starts);

    [Fact]
    public void Before_the_sales_window_opens_it_is_not_yet_on_sale()
        => Assert.Equal(SalesStatus.NotYetOnSale, Subject().SalesStatusAt(Opens.AddTicks(-1)));

    [Fact]
    public void Exactly_at_the_opening_instant_it_is_on_sale()
        => Assert.Equal(SalesStatus.OnSale, Subject().SalesStatusAt(Opens));

    [Fact]
    public void One_tick_before_closing_it_is_still_on_sale()
        => Assert.Equal(SalesStatus.OnSale, Subject().SalesStatusAt(Closes.AddTicks(-1)));

    [Fact]
    public void Exactly_at_the_closing_instant_sales_are_closed()
        => Assert.Equal(SalesStatus.SalesClosed, Subject().SalesStatusAt(Closes));

    [Fact]
    public void An_admin_pause_wins_over_an_open_sales_window()
    {
        var performance = Subject();
        performance.PauseSales();

        Assert.Equal(SalesStatus.Paused, performance.SalesStatusAt(TestData.Now));
    }

    [Fact]
    public void An_admin_pause_wins_even_before_the_window_opens()
    {
        var performance = Subject();
        performance.PauseSales();

        Assert.Equal(SalesStatus.Paused, performance.SalesStatusAt(Opens.AddDays(-10)));
    }

    [Fact]
    public void Resuming_restores_the_time_based_status()
    {
        var performance = Subject();
        performance.PauseSales();
        performance.ResumeSales();

        Assert.Equal(SalesStatus.OnSale, performance.SalesStatusAt(TestData.Now));
    }

    [Fact]
    public void Sales_window_must_be_opens_then_closes_then_starts()
    {
        var ex = Assert.Throws<BookingRuleException>(() =>
            TestData.Performance(opensAt: Closes, closesAt: Opens, startsAt: Starts));

        Assert.Equal(ErrorCode.ValidationFailed, ex.Code);
    }

    [Fact]
    public void Sales_cannot_close_after_the_performance_starts()
        => Assert.Throws<BookingRuleException>(() =>
            TestData.Performance(opensAt: Opens, closesAt: Starts.AddDays(1), startsAt: Starts));

    [Fact]
    public void Rescheduling_shifts_start_and_close_and_reopens_sales()
    {
        var performance = Subject();
        var shift = TimeSpan.FromDays(14);
        var newOpens = TestData.Now.AddDays(-1);

        performance.Reschedule(shift, newOpens);

        Assert.Equal(Starts + shift, performance.StartsAtUtc);
        Assert.Equal(Closes + shift, performance.SalesClosesAtUtc);
        Assert.Equal(newOpens, performance.SalesOpensAtUtc);
        Assert.Equal(SalesStatus.OnSale, performance.SalesStatusAt(TestData.Now));
    }

    [Fact]
    public void Rescheduling_that_would_break_the_time_ordering_is_rejected()
    {
        var performance = Subject();

        Assert.Throws<BookingRuleException>(() =>
            performance.Reschedule(TimeSpan.Zero, newSalesOpensAtUtc: Starts.AddDays(1)));
    }
}
