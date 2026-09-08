using System.Text.Json;
using NSubstitute;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Booking;
using Ticketing.Application.Booking.Dtos;
using Ticketing.Domain.Booking;
using Ticketing.Domain.Catalog;
using Ticketing.Domain.Common;
using Ticketing.Domain.Orders;

namespace Ticketing.Application.Tests;

/// <summary>
/// A01～A04 與保留／付款流程的分支（設計文件 10 第 2.1 節）。
///
/// 這一層**不連資料庫**，所以它證明不了「鎖真的把人排開了」。
/// 它證明的是另一件同樣重要的事：**在每一種情況下，我們有沒有呼叫該呼叫的東西、
/// 有沒有避免呼叫不該呼叫的東西**。很多真實的 bug 是後者。
/// </summary>
public class BookingServiceTests
{
    private readonly BookingTestContext _ = new();

    // ── A01：條件更新少一列 ───────────────────────────────────────────

    [Fact] // A01
    public async Task When_the_conditional_update_touches_fewer_rows_the_whole_thing_is_rejected()
    {
        var seats = new[] { BookingTestContext.SeatOf(1101, 1, 1), BookingTestContext.SeatOf(1102, 1, 2) };
        _.ArrangeConcertOnSale(seats);

        // 兩席只搶到一席——代表另一席在我們讀完之後被別人拿走了
        _.Seats.TryHoldAsync(1, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<Guid>(),
                             Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
               .Returns(1);

        var error = await Assert.ThrowsAsync<BookingRuleException>(() => CreateHoldAsync(1101, 1102));

        Assert.Equal(ErrorCode.SeatUnavailable, error.Code);

        // 沒有成功就不能留下冪等紀錄，否則使用者重送會拿到一個從未成立的「成功」
        _.Idempotency.DidNotReceive().Add(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<byte[]>(),
                                          Arg.Any<int>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>());
    }

    // ── A02：剛好到期時付款 ───────────────────────────────────────────

    [Fact] // A02
    public async Task Checking_out_at_the_exact_expiry_instant_is_rejected()
    {
        // 建立時間往前推 5 分鐘 → ExpiresAtUtc 剛好等於現在。
        // 「剛好那一秒」是最重要的邊界：>= 或 > 差一個字元，行為完全不同。
        var hold = BookingTestContext.ActiveHold(BookingTestContext.Now.AddMinutes(-5),
                                                 BookingTestContext.SeatOf(1101, 1, 1));
        ArrangeHold(hold);

        var error = await Assert.ThrowsAsync<BookingRuleException>(() => CheckoutAsync(CheckoutOutcome.Succeeded));

        Assert.Equal(ErrorCode.HoldExpired, error.Code);
        _.Orders.DidNotReceive().Add(Arg.Any<Order>());
        await _.Seats.DidNotReceive().MarkSoldAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── A03：演唱會要求連號 ───────────────────────────────────────────

    [Fact] // A03
    public async Task A_concert_cannot_ask_for_automatic_contiguous_allocation()
    {
        _.ArrangeConcertOnSale();

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            _.Service().CreateHoldAsync(BookingTestContext.Buyer, 1,
                                        BookingTestContext.ContiguousRequest(2),
                                        BookingTestContext.Key, CancellationToken.None));

        Assert.Equal(ErrorCode.ValidationFailed, error.Code);

        // 政策不允許就不該去碰座位——連查都不用查
        Assert.Empty(_.Seats.ReceivedCalls());
    }

    // ── A04：同 key 不同內容 ─────────────────────────────────────────

    [Fact] // A04
    public async Task The_same_key_with_different_content_is_a_conflict_not_a_replay()
    {
        _.ArrangeConcertOnSale(BookingTestContext.SeatOf(1101, 1, 1));

        // 上一次用同一個 key 存的指紋是別的東西
        _.Idempotency.FindAsync(BookingTestContext.Buyer, BookingTestContext.Key, Arg.Any<CancellationToken>())
                     .Returns(new IdempotencyEntry([1, 2, 3], 201, "{}"));

        var error = await Assert.ThrowsAsync<BookingRuleException>(() => CreateHoldAsync(1101));

        Assert.Equal(ErrorCode.IdempotencyKeyReuse, error.Code);
        await _.Seats.DidNotReceive().TryHoldAsync(Arg.Any<int>(), Arg.Any<IReadOnlyCollection<int>>(),
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_same_key_with_the_same_content_replays_without_touching_any_seat()
    {
        _.ArrangeConcertOnSale(BookingTestContext.SeatOf(1101, 1, 1));

        var fingerprint = IdempotencyCommand.CreateHold(1, BookingTestContext.ManualRequest(1101));
        _.Idempotency.FindAsync(BookingTestContext.Buyer, BookingTestContext.Key, Arg.Any<CancellationToken>())
                     .Returns(new IdempotencyEntry(fingerprint, 201, """{"id":"stored"}"""));

        var response = await CreateHoldAsync(1101);

        Assert.True(response.IsReplay);
        Assert.Equal(201, response.HttpStatus);
        Assert.Equal("""{"id":"stored"}""", response.ResponseJson);   // 逐位元原樣重播
        Assert.Empty(_.Seats.ReceivedCalls());
    }

    [Fact]
    public void The_same_seats_in_a_different_order_are_the_same_request()
    {
        var ascending = IdempotencyCommand.CreateHold(1, BookingTestContext.ManualRequest(1101, 1102));
        var descending = IdempotencyCommand.CreateHold(1, BookingTestContext.ManualRequest(1102, 1101));

        Assert.Equal(ascending, descending);

        // ……但不同場次的同一個 body 一定是不同的請求：
        // 只 hash body 的話，同一個 key 就會把 A 場的訂單回給 B 場
        Assert.NotEqual(ascending, IdempotencyCommand.CreateHold(2, BookingTestContext.ManualRequest(1101, 1102)));
    }

    // ── 輸入驗證：在開交易之前就擋掉 ─────────────────────────────────

    [Theory]
    [InlineData(3, new[] { 1101, 1102 })]        // 張數與座位數不符
    [InlineData(2, new[] { 1101, 1101 })]        // 重複座位
    public async Task Malformed_selection_is_rejected_before_a_transaction_is_even_opened(
        int quantity, int[] seatIds)
    {
        var request = new CreateHoldRequest
        {
            SectionId = 11, Quantity = quantity, SelectionMode = SelectionMode.Manual, SeatIds = seatIds
        };

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            _.Service().CreateHoldAsync(BookingTestContext.Buyer, 1, request,
                                        BookingTestContext.Key, CancellationToken.None));

        Assert.Equal(ErrorCode.ValidationFailed, error.Code);

        // 形狀不對的請求連交易都不該開。重複座位尤其重要：
        // 指紋會先排序，沒先擋掉的話 [1,1] 與 [1] 會得到同一個指紋
        Assert.Empty(_.Uow.ReceivedCalls());
    }

    [Fact]
    public async Task Sales_that_are_not_open_are_rejected_before_any_seat_is_touched()
    {
        var paused = BookingTestContext.PerformanceOf(BookingTestContext.ConcertEvent());
        paused.PauseSales();
        _.Performances.GetPublishedAsync(1, Arg.Any<CancellationToken>()).Returns(paused);

        var error = await Assert.ThrowsAsync<BookingRuleException>(() => CreateHoldAsync(1101));

        Assert.Equal(ErrorCode.NotOnSale, error.Code);
        Assert.Empty(_.Seats.ReceivedCalls());
    }

    // ── 每人上限與既有保留 ───────────────────────────────────────────

    [Fact]
    public async Task Already_paid_seats_count_towards_the_per_buyer_limit()
    {
        _.ArrangeConcertOnSale(BookingTestContext.SeatOf(1101, 1, 1));
        _.Orders.CountPaidSeatsAsync(BookingTestContext.Buyer, 1, Arg.Any<CancellationToken>()).Returns(2);

        // 演唱會上限 4：已付 2 ＋ 這次 3 = 5
        var request = BookingTestContext.ManualRequest(1101, 1102, 1103);
        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            _.Service().CreateHoldAsync(BookingTestContext.Buyer, 1, request,
                                        BookingTestContext.Key, CancellationToken.None));

        Assert.Equal(ErrorCode.LimitExceeded, error.Code);
    }

    [Fact]
    public async Task A_live_hold_blocks_a_second_one_and_the_error_says_which_hold()
    {
        _.ArrangeConcertOnSale(BookingTestContext.SeatOf(1101, 1, 1));

        var live = BookingTestContext.ActiveHold(BookingTestContext.Now.AddMinutes(-1),
                                                 BookingTestContext.SeatOf(1105, 1, 5));
        _.Holds.FindActiveAsync(BookingTestContext.Buyer, 1, Arg.Any<CancellationToken>()).Returns(live);

        var error = await Assert.ThrowsAsync<BookingRuleException>(() => CreateHoldAsync(1101));

        Assert.Equal(ErrorCode.ActiveHoldExists, error.Code);

        // 前端要知道該把人導去哪一筆，不然使用者只會看到「你已經有保留了」卻找不到它
        Assert.NotNull(error.Extensions);
        Assert.Equal(live.Id, error.Extensions["holdId"]);
    }

    [Fact]
    public async Task An_expired_hold_of_my_own_is_tidied_up_before_the_new_one_is_created()
    {
        var seat = BookingTestContext.SeatOf(1101, 1, 1);
        _.ArrangeConcertOnSale(seat);

        // 資料庫狀態還是 Active，但時間已經過了——它會擋住 filtered unique index
        var stale = BookingTestContext.ActiveHold(BookingTestContext.Now.AddMinutes(-10),
                                                  BookingTestContext.SeatOf(1105, 1, 5));
        _.Holds.FindActiveAsync(BookingTestContext.Buyer, 1, Arg.Any<CancellationToken>()).Returns(stale);

        var response = await CreateHoldAsync(1101);

        Assert.Equal(201, response.HttpStatus);
        Assert.Equal(HoldStatus.Expired, stale.Status);                    // 先轉成 Expired
        await _.Seats.Received().ReleaseAsync(stale.Id, Arg.Any<CancellationToken>());   // 再放座位
    }

    // ── 連號配位 ─────────────────────────────────────────────────────

    [Fact]
    public async Task A_sport_event_can_ask_for_contiguous_seats_and_gets_the_first_run()
    {
        _.Performances.GetPublishedAsync(1, Arg.Any<CancellationToken>())
                      .Returns(BookingTestContext.PerformanceOf(BookingTestContext.SportEvent()));
        _.Performances.GetSectionAsync(1, 11, Arg.Any<CancellationToken>()).Returns(BookingTestContext.SectionA());
        _.Holds.FindActiveAsync(BookingTestContext.Buyer, 1, Arg.Any<CancellationToken>()).Returns((SeatHold?)null);
        _.Orders.CountPaidSeatsAsync(BookingTestContext.Buyer, 1, Arg.Any<CancellationToken>()).Returns(0);

        // 第 1 排只剩 1、3（不連續），第 2 排有 1、2 → 應該配到第 2 排
        _.Seats.GetAvailableInSectionAsync(11, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
               .Returns([
                   BookingTestContext.SeatOf(1101, 1, 1), BookingTestContext.SeatOf(1103, 1, 3),
                   BookingTestContext.SeatOf(1201, 2, 1), BookingTestContext.SeatOf(1202, 2, 2)]);
        _.Seats.TryHoldAsync(1, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<Guid>(),
                             Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
               .Returns(2);

        var response = await _.Service().CreateHoldAsync(BookingTestContext.Buyer, 1,
            BookingTestContext.ContiguousRequest(2), BookingTestContext.Key, CancellationToken.None);

        var dto = JsonSerializer.Deserialize<HoldDto>(response.ResponseJson,
                                                      Application.Common.PublicJson.Options)!;
        Assert.Equal([1201, 1202], dto.Items.Select(i => i.SeatId).ToArray());
    }

    [Fact]
    public async Task When_no_run_is_long_enough_it_says_so_instead_of_picking_scattered_seats()
    {
        _.Performances.GetPublishedAsync(1, Arg.Any<CancellationToken>())
                      .Returns(BookingTestContext.PerformanceOf(BookingTestContext.SportEvent()));
        _.Performances.GetSectionAsync(1, 11, Arg.Any<CancellationToken>()).Returns(BookingTestContext.SectionA());
        _.Seats.GetAvailableInSectionAsync(11, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
               .Returns([BookingTestContext.SeatOf(1101, 1, 1), BookingTestContext.SeatOf(1103, 1, 3)]);

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            _.Service().CreateHoldAsync(BookingTestContext.Buyer, 1,
                BookingTestContext.ContiguousRequest(2), BookingTestContext.Key, CancellationToken.None));

        Assert.Equal(ErrorCode.NoContiguousSeats, error.Code);
    }

    // ── 付款 ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_mock_failure_changes_nothing_and_can_be_retried_with_a_new_key()
    {
        var hold = BookingTestContext.ActiveHold(BookingTestContext.Now.AddMinutes(-1),
                                                 BookingTestContext.SeatOf(1101, 1, 1));
        ArrangeHold(hold);

        var response = await CheckoutAsync(CheckoutOutcome.Failed);

        Assert.Equal(402, response.HttpStatus);
        Assert.Equal(HoldStatus.Active, hold.Status);            // 保留完全沒動
        Assert.Equal(BookingTestContext.Now.AddMinutes(4), hold.ExpiresAtUtc);   // 到期時間也沒被延長
        await _.Seats.DidNotReceive().MarkSoldAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        _.Orders.DidNotReceive().Add(Arg.Any<Order>());
    }

    [Fact]
    public async Task A_successful_checkout_marks_the_seats_sold_and_creates_one_order()
    {
        var hold = BookingTestContext.ActiveHold(BookingTestContext.Now.AddMinutes(-1),
                                                 BookingTestContext.SeatOf(1101, 1, 1),
                                                 BookingTestContext.SeatOf(1102, 1, 2));
        ArrangeHold(hold);
        _.Seats.MarkSoldAsync(hold.Id, Arg.Any<CancellationToken>()).Returns(2);

        var response = await CheckoutAsync(CheckoutOutcome.Succeeded);

        Assert.Equal(201, response.HttpStatus);
        Assert.Equal(HoldStatus.Completed, hold.Status);
        _.Orders.Received(1).Add(Arg.Is<Order>(o => o.Items.Count == 2 && o.TotalAmount == 4200m));
    }

    [Fact]
    public async Task If_the_seats_are_no_longer_all_ours_the_payment_is_rolled_back()
    {
        var hold = BookingTestContext.ActiveHold(BookingTestContext.Now.AddMinutes(-1),
                                                 BookingTestContext.SeatOf(1101, 1, 1),
                                                 BookingTestContext.SeatOf(1102, 1, 2));
        ArrangeHold(hold);
        _.Seats.MarkSoldAsync(hold.Id, Arg.Any<CancellationToken>()).Returns(1);   // 只剩一席是我們的

        var error = await Assert.ThrowsAsync<BookingRuleException>(() => CheckoutAsync(CheckoutOutcome.Succeeded));

        // 絕不能出現「訂單成立了但座位還可以賣」
        Assert.Equal(ErrorCode.HoldExpired, error.Code);
        _.Orders.DidNotReceive().Add(Arg.Any<Order>());
    }

    [Fact]
    public async Task Paying_twice_returns_the_first_order_with_200_instead_of_creating_a_second()
    {
        var hold = BookingTestContext.ActiveHold(BookingTestContext.Now.AddMinutes(-1),
                                                 BookingTestContext.SeatOf(1101, 1, 1));
        hold.Complete(BookingTestContext.Now);
        ArrangeHold(hold);

        var existing = Order.FromHold(Guid.NewGuid(), hold,
            BookingTestContext.PerformanceOf(BookingTestContext.ConcertEvent()), BookingTestContext.Now);
        _.Orders.GetByHoldAsync(hold.Id, Arg.Any<CancellationToken>()).Returns(existing);

        var response = await CheckoutAsync(CheckoutOutcome.Succeeded);

        Assert.Equal(200, response.HttpStatus);                  // 200「已經有了」，不是 201「剛建立」
        _.Orders.DidNotReceive().Add(Arg.Any<Order>());
    }

    [Fact]
    public async Task A_completed_hold_without_an_order_is_reported_not_faked_into_a_success()
    {
        var hold = BookingTestContext.ActiveHold(BookingTestContext.Now.AddMinutes(-1),
                                                 BookingTestContext.SeatOf(1101, 1, 1));
        hold.Complete(BookingTestContext.Now);
        ArrangeHold(hold);
        _.Orders.GetByHoldAsync(hold.Id, Arg.Any<CancellationToken>()).Returns((Order?)null);

        // 這是資料不變量失敗。對 null 建 DTO 或回一個空訂單，等於把嚴重問題藏起來
        await Assert.ThrowsAsync<InvalidOperationException>(() => CheckoutAsync(CheckoutOutcome.Succeeded));
    }

    // ── 取消 ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancelling_releases_the_seats_and_cancelling_again_is_a_no_op()
    {
        var hold = BookingTestContext.ActiveHold(BookingTestContext.Now.AddMinutes(-1),
                                                 BookingTestContext.SeatOf(1101, 1, 1));
        ArrangeHold(hold);

        var first = await _.Service().CancelHoldAsync(BookingTestContext.Buyer, hold.Id, CancellationToken.None);
        Assert.Equal(HoldStatus.Cancelled, first.Status);
        await _.Seats.Received(1).ReleaseAsync(hold.Id, Arg.Any<CancellationToken>());

        var second = await _.Service().CancelHoldAsync(BookingTestContext.Buyer, hold.Id, CancellationToken.None);

        // 重送同樣回 200，而且**不會再放一次座位**——期間座位可能已經是別人的了
        Assert.Equal(HoldStatus.Cancelled, second.Status);
        await _.Seats.Received(1).ReleaseAsync(hold.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_completed_hold_cannot_be_cancelled()
    {
        var hold = BookingTestContext.ActiveHold(BookingTestContext.Now.AddMinutes(-1),
                                                 BookingTestContext.SeatOf(1101, 1, 1));
        hold.Complete(BookingTestContext.Now);
        ArrangeHold(hold);

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            _.Service().CancelHoldAsync(BookingTestContext.Buyer, hold.Id, CancellationToken.None));

        Assert.Equal(ErrorCode.HoldNotActive, error.Code);
        await _.Seats.DidNotReceive().ReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Someone_elses_hold_is_not_found_rather_than_forbidden()
    {
        _.Holds.FindPerformanceForBuyerAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
               .Returns((int?)null);

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            _.Service().CancelHoldAsync(BookingTestContext.Buyer, Guid.NewGuid(), CancellationToken.None));

        // 403 等於承認「這個東西存在，只是不給你看」
        Assert.Equal(ErrorCode.NotFound, error.Code);
    }

    // ── /me/holds ────────────────────────────────────────────────────

    [Fact]
    public async Task An_expired_hold_is_not_reported_as_an_active_one()
    {
        _.Performances.GetPublishedAsync(1, Arg.Any<CancellationToken>())
                      .Returns(BookingTestContext.PerformanceOf(BookingTestContext.ConcertEvent()));

        var stale = BookingTestContext.ActiveHold(BookingTestContext.Now.AddMinutes(-10),
                                                  BookingTestContext.SeatOf(1101, 1, 1));
        _.Holds.FindActiveAsync(BookingTestContext.Buyer, 1, Arg.Any<CancellationToken>()).Returns(stale);

        var result = await _.Service().GetActiveHoldsAsync(BookingTestContext.Buyer, 1, CancellationToken.None);

        // 資料庫是 Active，但前端不該被導去一筆已經沒用的保留
        Assert.Empty(result.Items);
    }

    // ── helpers ──────────────────────────────────────────────────────

    private Task<IdempotencyResponse> CreateHoldAsync(params int[] seatIds)
        => _.Service().CreateHoldAsync(BookingTestContext.Buyer, 1,
                                       BookingTestContext.ManualRequest(seatIds),
                                       BookingTestContext.Key, CancellationToken.None);

    private Task<IdempotencyResponse> CheckoutAsync(CheckoutOutcome outcome)
        => _.Service().CheckoutAsync(BookingTestContext.Buyer,
                                     Guid.Parse("33333333-3333-4333-8333-333333333333"),
                                     new CheckoutRequest { Outcome = outcome },
                                     BookingTestContext.Key, CancellationToken.None);

    private void ArrangeHold(SeatHold hold)
    {
        _.Holds.FindPerformanceForBuyerAsync(hold.Id, BookingTestContext.Buyer, Arg.Any<CancellationToken>())
               .Returns(1);
        _.Holds.GetForBuyerAsync(hold.Id, BookingTestContext.Buyer, Arg.Any<CancellationToken>()).Returns(hold);
        _.Performances.GetAsync(1, Arg.Any<CancellationToken>())
                      .Returns(BookingTestContext.PerformanceOf(BookingTestContext.ConcertEvent()));
    }
}
