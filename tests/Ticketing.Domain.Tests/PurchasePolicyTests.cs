using Ticketing.Domain.Booking;
using Ticketing.Domain.Catalog;
using Ticketing.Domain.Common;

namespace Ticketing.Domain.Tests;

public class PurchasePolicyTests
{
    [Fact]
    public void Concert_allows_four_tickets_and_no_contiguous_allocation()
    {
        var policy = PurchasePolicy.For(EventCategory.Concert);

        Assert.IsType<ConcertPurchasePolicy>(policy);
        Assert.Equal(4, policy.MaxTicketsPerBuyer);
        Assert.False(policy.AllowsContiguousAllocation);
    }

    [Fact]
    public void Sport_allows_six_tickets_and_contiguous_allocation()
    {
        var policy = PurchasePolicy.For(EventCategory.Sport);

        Assert.IsType<SportsPurchasePolicy>(policy);
        Assert.Equal(6, policy.MaxTicketsPerBuyer);
        Assert.True(policy.AllowsContiguousAllocation);
    }

    [Fact]
    public void Unknown_category_is_rejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => PurchasePolicy.For((EventCategory)99));

    [Theory]
    [InlineData(0, 4)]     // 一次買滿
    [InlineData(2, 2)]     // 已買 2 再買 2 剛好到上限
    [InlineData(3, 1)]
    public void Concert_within_limit_passes(int alreadyPaid, int requested)
        => PurchasePolicy.For(EventCategory.Concert).EnsureWithinLimit(alreadyPaid, requested);

    [Fact]
    public void Exceeding_the_per_buyer_limit_across_orders_throws_LimitExceeded()
    {
        var ex = Assert.Throws<BookingRuleException>(() =>
            PurchasePolicy.For(EventCategory.Concert).EnsureWithinLimit(alreadyPaid: 3, requested: 2));

        Assert.Equal(ErrorCode.LimitExceeded, ex.Code);
        Assert.Contains("3", ex.Message, StringComparison.Ordinal);   // 訊息要說出已買幾張
    }

    [Fact]
    public void Requesting_more_than_the_limit_in_one_go_is_a_validation_failure_not_a_limit_conflict()
    {
        // 單次就超過上限是「輸入不合法」（400），不是「你買太多了」（409）——兩者的 HTTP 語意不同
        var ex = Assert.Throws<BookingRuleException>(() =>
            PurchasePolicy.For(EventCategory.Concert).EnsureWithinLimit(alreadyPaid: 0, requested: 5));

        Assert.Equal(ErrorCode.ValidationFailed, ex.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_quantity_is_rejected(int requested)
    {
        var ex = Assert.Throws<BookingRuleException>(() =>
            PurchasePolicy.For(EventCategory.Sport).EnsureWithinLimit(0, requested));

        Assert.Equal(ErrorCode.ValidationFailed, ex.Code);
    }

    [Fact]
    public void Sport_buyer_may_take_six_where_a_concert_buyer_may_not()
    {
        PurchasePolicy.For(EventCategory.Sport).EnsureWithinLimit(0, 6);

        Assert.Throws<BookingRuleException>(() =>
            PurchasePolicy.For(EventCategory.Concert).EnsureWithinLimit(0, 6));
    }
}
