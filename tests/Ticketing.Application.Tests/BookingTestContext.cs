using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
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
/// <see cref="BookingService"/> 的替身組。
///
/// 六個相依全部是替身，包含 <see cref="IUnitOfWork"/>——它的
/// <c>ExecuteInTransactionAsync</c> 直接執行委派，**不開真交易**。
/// 這一層要證明的是「流程走對」；「鎖真的鎖住了」只有真 SQL Server 能證明，那是 T 系列的事
/// （設計文件 10 第 2.1 節）。
/// </summary>
internal sealed class BookingTestContext
{
    public static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
    public static readonly Guid Buyer = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public const string Key = "22222222-2222-4222-8222-222222222222";

    public IUnitOfWork Uow { get; } = Substitute.For<IUnitOfWork>();
    public IBookingWriteGate Gate { get; } = Substitute.For<IBookingWriteGate>();
    public IPerformanceRepository Performances { get; } = Substitute.For<IPerformanceRepository>();
    public ISeatRepository Seats { get; } = Substitute.For<ISeatRepository>();
    public ISeatHoldRepository Holds { get; } = Substitute.For<ISeatHoldRepository>();
    public IOrderRepository Orders { get; } = Substitute.For<IOrderRepository>();
    public IIdempotencyDao Idempotency { get; } = Substitute.For<IIdempotencyDao>();
    public FakeTimeProvider Clock { get; } = new(Now);

    public BookingTestContext()
    {
        // 交易替身：直接跑委派。真交易與重試由 UnitOfWork 負責，那是 Infrastructure 的測試範圍。
        Uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<IdempotencyResponse>>>(),
                                      Arg.Any<CancellationToken>())
           .Returns(call => ((Func<CancellationToken, Task<IdempotencyResponse>>)call[0])(CancellationToken.None));

        Uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<HoldDto>>>(),
                                      Arg.Any<CancellationToken>())
           .Returns(call => ((Func<CancellationToken, Task<HoldDto>>)call[0])(CancellationToken.None));

        Gate.EnterPerformanceReadAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        Gate.EnterBuyerAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    public BookingService Service() => new(Uow, Gate, Performances, Seats, Holds, Orders,
                                           new IdempotencyGuard(Idempotency), Clock,
                                           NullLogger<BookingService>.Instance);

    // ── 測試資料 ──────────────────────────────────────────────────────

    public static Event ConcertEvent(bool published = true)
        => new(1, "C01", EventCategory.Concert, "夜藍之後", "林予晞", "流行", "說明",
               "/assets/events/C01.png", published);

    public static Event SportEvent()
        => new(2, "S01", EventCategory.Sport, "港都海鷗 vs 山城野牛", "兩隊", "棒球", "說明",
               "/assets/events/S01.png");

    public static Performance PerformanceOf(Event source, int id = 1)
        => new(id, source, "小巨蛋", "台北",
               Now.AddDays(30), Now.AddDays(-1), Now.AddDays(29), 150);

    public static Section SectionA(int performanceId = 1) => new(11, performanceId, "A", "A 區", 2100m, 5, 12);

    public static Seat SeatOf(int id, int row, int number, int performanceId = 1, int sectionId = 11)
        => new(id, performanceId, sectionId, row, number);

    public static SeatHold ActiveHold(DateTimeOffset createdAt, params Seat[] seats)
        => new(Guid.Parse("33333333-3333-4333-8333-333333333333"), Buyer, 1,
               [.. seats.Select(seat => new SeatHoldItem(seat, SectionA()))],
               createdAt, TimeSpan.FromMinutes(5));

    public static CreateHoldRequest ManualRequest(params int[] seatIds)
        => new() { SectionId = 11, Quantity = seatIds.Length, SelectionMode = SelectionMode.Manual, SeatIds = seatIds };

    public static CreateHoldRequest ContiguousRequest(int quantity, int sectionId = 11)
        => new() { SectionId = sectionId, Quantity = quantity, SelectionMode = SelectionMode.Contiguous, SeatIds = [] };

    /// <summary>「一切正常」的預設接線：場次開賣、票區存在、沒有舊保留、沒有已付款。</summary>
    public void ArrangeConcertOnSale(params Seat[] seats)
    {
        Performances.GetPublishedAsync(1, Arg.Any<CancellationToken>())
                    .Returns(PerformanceOf(ConcertEvent()));
        Performances.GetSectionAsync(1, 11, Arg.Any<CancellationToken>()).Returns(SectionA());
        Holds.FindActiveAsync(Buyer, 1, Arg.Any<CancellationToken>()).Returns((SeatHold?)null);
        Orders.CountPaidSeatsAsync(Buyer, 1, Arg.Any<CancellationToken>()).Returns(0);

        if (seats.Length == 0) return;

        Seats.GetManyAsync(1, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
             .Returns(seats);
        Seats.TryHoldAsync(1, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<Guid>(),
                           Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
             .Returns(seats.Length);
    }
}
