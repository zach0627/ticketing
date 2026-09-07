using Ticketing.Domain.Booking;
using Ticketing.Domain.Common;

namespace Ticketing.Domain.Tests;

public class SeatHoldTests
{
    // ── 建構 ──────────────────────────────────────────────────────

    [Fact]
    public void Total_amount_is_computed_by_the_domain_not_supplied_by_the_caller()
    {
        var section = TestData.Section(price: 3200m);
        var items = new[] { TestData.Item(1101, 1, 1, section), TestData.Item(1102, 1, 2, section) };

        var hold = new SeatHold(Guid.NewGuid(), Guid.NewGuid(), 1, items, TestData.Now, TestData.HoldFor);

        Assert.Equal(6400m, hold.TotalAmount);
    }

    [Fact]
    public void Expiry_is_fixed_at_creation_time()
    {
        var hold = TestData.Hold(now: TestData.Now, holdFor: TimeSpan.FromMinutes(5));

        Assert.Equal(TestData.Now, hold.CreatedAtUtc);
        Assert.Equal(TestData.Now.AddMinutes(5), hold.ExpiresAtUtc);
        Assert.Equal(HoldStatus.Active, hold.Status);
    }

    [Fact]
    public void Empty_seat_list_is_rejected()
    {
        var ex = Assert.Throws<BookingRuleException>(() =>
            new SeatHold(Guid.NewGuid(), Guid.NewGuid(), 1, [], TestData.Now, TestData.HoldFor));

        Assert.Equal(ErrorCode.ValidationFailed, ex.Code);
    }

    [Fact]
    public void Duplicate_seats_are_rejected()
    {
        var items = new[] { TestData.Item(1101, 1, 1), TestData.Item(1101, 1, 1) };

        var ex = Assert.Throws<BookingRuleException>(() =>
            new SeatHold(Guid.NewGuid(), Guid.NewGuid(), 1, items, TestData.Now, TestData.HoldFor));

        Assert.Equal(ErrorCode.ValidationFailed, ex.Code);
    }

    [Fact]
    public void Non_positive_hold_duration_is_rejected()
    {
        var items = new[] { TestData.Item(1101, 1, 1) };

        Assert.Throws<BookingRuleException>(() =>
            new SeatHold(Guid.NewGuid(), Guid.NewGuid(), 1, items, TestData.Now, TimeSpan.Zero));
    }

    [Fact]
    public void Items_cannot_be_mutated_through_the_public_surface()
    {
        var hold = TestData.Hold(seatCount: 2);

        Assert.IsNotType<List<SeatHoldItem>>(hold.Items);
        Assert.Equal(2, hold.Items.Count);
    }

    // ── 到期：邊界是這裡最重要的一條 ────────────────────────────────

    [Fact]
    public void One_tick_before_expiry_is_not_expired()
        => Assert.False(TestData.Hold().IsExpiredAt(TestData.Now.AddMinutes(5).AddTicks(-1)));

    [Fact]
    public void Exactly_at_expiry_counts_as_expired()
        => Assert.True(TestData.Hold().IsExpiredAt(TestData.Now.AddMinutes(5)));

    [Fact]
    public void StatusAt_reports_expired_even_though_the_stored_status_is_still_active()
    {
        var hold = TestData.Hold();

        Assert.Equal(HoldStatus.Active, hold.Status);                               // 資料庫欄位
        Assert.Equal(HoldStatus.Expired, hold.StatusAt(TestData.Now.AddMinutes(5))); // 對外顯示
    }

    [Fact]
    public void StatusAt_does_not_change_terminal_states()
    {
        var hold = TestData.Hold();
        hold.Cancel(TestData.Now);

        Assert.Equal(HoldStatus.Cancelled, hold.StatusAt(TestData.Now.AddHours(1)));
    }

    // ── 付款 ──────────────────────────────────────────────────────

    [Fact]
    public void Checkout_is_allowed_one_tick_before_expiry()
    {
        var hold = TestData.Hold();

        hold.Complete(TestData.Now.AddMinutes(5).AddTicks(-1));

        Assert.Equal(HoldStatus.Completed, hold.Status);
    }

    [Fact]
    public void Checkout_at_the_expiry_instant_throws_HoldExpired()
    {
        var hold = TestData.Hold();

        var ex = Assert.Throws<BookingRuleException>(() => hold.Complete(TestData.Now.AddMinutes(5)));

        Assert.Equal(ErrorCode.HoldExpired, ex.Code);
        Assert.Equal(HoldStatus.Active, hold.Status);       // 失敗不改狀態
    }

    [Fact]
    public void Checkout_of_a_cancelled_hold_throws_HoldNotActive()
    {
        var hold = TestData.Hold();
        hold.Cancel(TestData.Now);

        var ex = Assert.Throws<BookingRuleException>(() => hold.Complete(TestData.Now));

        Assert.Equal(ErrorCode.HoldNotActive, ex.Code);
    }

    [Fact]
    public void Checkout_of_a_completed_hold_throws_HoldNotActive()
    {
        var hold = TestData.Hold();
        hold.Complete(TestData.Now);

        var ex = Assert.Throws<BookingRuleException>(() => hold.Complete(TestData.Now));

        Assert.Equal(ErrorCode.HoldNotActive, ex.Code);
    }

    [Fact]
    public void EnsureCheckoutAllowed_does_not_change_state()
    {
        var hold = TestData.Hold();

        hold.EnsureCheckoutAllowed(TestData.Now);

        Assert.Equal(HoldStatus.Active, hold.Status);
    }

    // ── 取消 ──────────────────────────────────────────────────────

    [Fact]
    public void Cancelling_an_active_hold_before_expiry_marks_it_cancelled()
    {
        var hold = TestData.Hold();

        hold.Cancel(TestData.Now.AddMinutes(1));

        Assert.Equal(HoldStatus.Cancelled, hold.Status);
    }

    [Fact]
    public void Cancelling_after_expiry_marks_it_expired_not_cancelled()
    {
        var hold = TestData.Hold();

        hold.Cancel(TestData.Now.AddMinutes(5));

        Assert.Equal(HoldStatus.Expired, hold.Status);
    }

    [Fact]
    public void Cancelling_twice_is_idempotent()
    {
        var hold = TestData.Hold();
        hold.Cancel(TestData.Now);

        hold.Cancel(TestData.Now);                          // 不應該丟例外

        Assert.Equal(HoldStatus.Cancelled, hold.Status);
    }

    [Fact]
    public void A_completed_hold_cannot_be_cancelled()
    {
        var hold = TestData.Hold();
        hold.Complete(TestData.Now);

        var ex = Assert.Throws<BookingRuleException>(() => hold.Cancel(TestData.Now));

        Assert.Equal(ErrorCode.HoldNotActive, ex.Code);
        Assert.Equal(HoldStatus.Completed, hold.Status);
    }
}
