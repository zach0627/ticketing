using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Admin;
using Ticketing.Application.Admin.Dtos;
using Ticketing.Application.Common;
using Ticketing.Domain.Catalog;
using Ticketing.Domain.Common;

namespace Ticketing.Application.Tests;

/// <summary>
/// A06 與管理操作的流程分支（設計文件 10 第 2.1 節、14）。
/// </summary>
public class AdminServiceTests
{
    private static readonly DateTimeOffset Now = BookingTestContext.Now;
    private static readonly Guid Actor = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid Operation = Guid.Parse("55555555-5555-4555-8555-555555555555");

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IBookingWriteGate _gate = Substitute.For<IBookingWriteGate>();
    private readonly IAdminDao _dao = Substitute.For<IAdminDao>();
    private readonly IPerformanceRepository _performances = Substitute.For<IPerformanceRepository>();
    private readonly FakeTimeProvider _clock = new(Now);

    public AdminServiceTests()
    {
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<AdminOperationResponse>>>(),
                                       Arg.Any<CancellationToken>())
            .Returns(call => ((Func<CancellationToken, Task<AdminOperationResponse>>)call[0])(CancellationToken.None));

        _gate.EnterPerformanceWriteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);

        _dao.CountPurchasesAsync(Arg.Any<CancellationToken>()).Returns(new PurchaseCounts(3, 2));
        _dao.GetEarliestPerformanceStartAsync(Arg.Any<CancellationToken>()).Returns(Now.AddDays(-5));
        _dao.ReleaseAllSeatsAsync(Arg.Any<CancellationToken>()).Returns(120);
    }

    private AdminService Service() =>
        new(_uow, _gate, _dao, _performances, _clock, NullLogger<AdminService>.Instance);

    // ── A06 ───────────────────────────────────────────────────────────

    [Fact] // A06
    public async Task If_releasing_the_seats_fails_the_audit_is_never_written()
    {
        _dao.ReleaseAllSeatsAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("資料庫壞掉了"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service().ResetAsync(Actor, Reset(), Operation, CancellationToken.None));

        // 稽核代表「這個操作完成了」。中途失敗卻留下稽核，重試就會被誤判成「已經做過了」
        _dao.DidNotReceive().AddAudit(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(),
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>());
    }

    // ── 重置 ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Reset_releases_seats_before_deleting_the_holds_they_point_at()
    {
        var response = await Service().ResetAsync(Actor, Reset(), Operation, CancellationToken.None);

        // 順序是外鍵決定的：Seats.HoldId 還指著 SeatHolds 時不能刪它
        Received.InOrder(() =>
        {
            _dao.ReleaseAllSeatsAsync(Arg.Any<CancellationToken>());
            _dao.DeleteAllPurchaseDataAsync(Arg.Any<CancellationToken>());
            _dao.ResetPerformanceScheduleAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset>(),
                                               Arg.Any<CancellationToken>());
        });

        var result = JsonSerializer.Deserialize<ResetResult>(response.ResponseJson, PublicJson.Options)!;
        Assert.Equal(3, result.OrdersDeleted);
        Assert.Equal(2, result.HoldsDeleted);
        Assert.Equal(120, result.SeatsReleased);
    }

    [Fact]
    public async Task Reset_shifts_dates_so_the_earliest_show_is_two_weeks_out()
    {
        // 最早的一場在 5 天前 → 要往後搬 19 天才會變成「今天 ＋ 14 天」
        await Service().ResetAsync(Actor, Reset(), Operation, CancellationToken.None);

        await _dao.Received().ResetPerformanceScheduleAsync(19, Now.AddDays(-1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reset_never_pulls_a_future_show_closer()
    {
        _dao.GetEarliestPerformanceStartAsync(Arg.Any<CancellationToken>()).Returns(Now.AddDays(90));

        await Service().ResetAsync(Actor, Reset(), Operation, CancellationToken.None);

        // 已經在很遠未來的場次不該被拉近，所以位移是 0 而不是負的
        await _dao.Received().ResetPerformanceScheduleAsync(0, Now.AddDays(-1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reset_without_any_performance_is_a_not_found()
    {
        _dao.GetEarliestPerformanceStartAsync(Arg.Any<CancellationToken>()).Returns((DateTimeOffset?)null);

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            Service().ResetAsync(Actor, Reset(), Operation, CancellationToken.None));

        Assert.Equal(ErrorCode.NotFound, error.Code);
        await _dao.DidNotReceive().DeleteAllPurchaseDataAsync(Arg.Any<CancellationToken>());
    }

    // ── 去重 ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Replaying_a_reset_returns_the_first_result_and_deletes_nothing_again()
    {
        var stored = """{"ordersDeleted":7,"holdsDeleted":1,"seatsReleased":9,"earliestPerformanceUtc":"2026-10-01T00:00:00+00:00"}""";
        ExistingOperation("Reset", stored, Reset());

        var response = await Service().ResetAsync(Actor, Reset(), Operation, CancellationToken.None);

        Assert.True(response.IsReplay);
        Assert.Equal(stored, response.ResponseJson);   // 回第一次的數字，不是「第二次刪了 0 筆」

        // 重播**什麼都不做**：不刪資料、不改日期、也不再寫一筆稽核
        await _dao.DidNotReceive().DeleteAllPurchaseDataAsync(Arg.Any<CancellationToken>());
        await _dao.DidNotReceive().ResetPerformanceScheduleAsync(Arg.Any<int>(), Arg.Any<DateTimeOffset>(),
                                                                 Arg.Any<CancellationToken>());
        _dao.DidNotReceive().AddAudit(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(),
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task The_same_operation_id_used_by_a_different_admin_is_a_conflict()
    {
        ExistingOperation("Reset", "{}", Reset(), actor: Guid.NewGuid());

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            Service().ResetAsync(Actor, Reset(), Operation, CancellationToken.None));

        Assert.Equal(ErrorCode.IdempotencyKeyReuse, error.Code);
    }

    [Fact]
    public async Task The_same_operation_id_used_for_a_different_action_is_a_conflict()
    {
        // 上一次這個 key 用在暫停售票，這次拿來重置——不是重送，是誤用
        ExistingOperation("SetSalesPaused", "{}", Reset());

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            Service().ResetAsync(Actor, Reset(), Operation, CancellationToken.None));

        Assert.Equal(ErrorCode.IdempotencyKeyReuse, error.Code);
    }

    // ── 暫停／恢復 ───────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Pausing_and_resuming_write_the_actual_resulting_state(bool paused)
    {
        var performance = BookingTestContext.PerformanceOf(BookingTestContext.ConcertEvent());
        if (!paused) performance.PauseSales();      // 先暫停，才測得出「恢復」
        _performances.GetAsync(1, Arg.Any<CancellationToken>()).Returns(performance);

        var response = await Service().SetSalesPausedAsync(
            Actor, 1, new PauseSalesRequest { IsSalesPaused = paused }, Operation, CancellationToken.None);

        var result = JsonSerializer.Deserialize<PauseSalesResult>(response.ResponseJson, PublicJson.Options)!;
        Assert.Equal(1, result.PerformanceId);
        Assert.Equal(paused, result.IsSalesPaused);
        Assert.Equal(paused, performance.IsSalesPaused);
    }

    [Fact]
    public async Task Pausing_takes_the_exclusive_gate_first_and_404s_when_the_show_is_gone()
    {
        _gate.EnterPerformanceWriteAsync(999, Arg.Any<CancellationToken>()).Returns(false);

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            Service().SetSalesPausedAsync(Actor, 999, new PauseSalesRequest { IsSalesPaused = true },
                                          Operation, CancellationToken.None));

        Assert.Equal(ErrorCode.NotFound, error.Code);
        await _performances.DidNotReceive().GetAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static ResetRequest Reset() => new() { Confirmation = "RESET" };

    /// <summary>用真的指紋建立一筆「已經做過的操作」。</summary>
    private void ExistingOperation(string action, string resultJson, ResetRequest request, Guid? actor = null)
    {
        // 指紋演算法是 AdminService 的內部細節，這裡用「跑一次真的流程再讀回來」的方式取得，
        // 避免在測試裡複製一份演算法——複製就會漂移
        byte[]? captured = null;
        _dao.When(d => d.AddAudit(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(),
                                  Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string>(),
                                  Arg.Any<DateTimeOffset>()))
            .Do(call => captured = (byte[])call[3]);

        Service().ResetAsync(Actor, request, Operation, CancellationToken.None).GetAwaiter().GetResult();

        _dao.ClearReceivedCalls();
        _dao.FindOperationAsync(Operation, Arg.Any<CancellationToken>())
            .Returns(new AdminOperationRecord(actor ?? Actor, action, captured!, resultJson));
    }
}
