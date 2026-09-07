using Ticketing.Domain.Booking;
using Ticketing.Domain.Catalog;

namespace Ticketing.Domain.Tests;

/// <summary>
/// 測試用的建構捷徑。刻意用固定值而不是隨機值，失敗時比較好重現。
/// 時間基準也固定：Domain 從不讀系統時鐘，測試也不該依賴它。
/// </summary>
internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    public static readonly TimeSpan HoldFor = TimeSpan.FromMinutes(5);

    public static Event Event(EventCategory category = EventCategory.Concert, string code = "C01")
        => new(1, code, category, "測試活動", "測試表演者", "流行", "介紹", $"/assets/events/{code}.png");

    public static Performance Performance(Event? @event = null, DateTimeOffset? opensAt = null,
                                          DateTimeOffset? closesAt = null, DateTimeOffset? startsAt = null)
        => new(1, @event ?? Event(), "測試場館", "台北",
               startsAt ?? Now.AddDays(30),
               opensAt ?? Now.AddDays(-1),
               closesAt ?? Now.AddDays(29),
               durationMinutes: 120);

    public static Section Section(int id = 11, decimal price = 3200m, int rows = 4, int seatsPerRow = 10)
        => new(id, performanceId: 1, code: "A", name: "A 區", price, rows, seatsPerRow);

    public static Seat Seat(int id, int row, int number, int sectionId = 11)
        => new(id, performanceId: 1, sectionId, row, number);

    public static Seat HeldSeat(int id, int row, int number, DateTimeOffset heldUntil, int sectionId = 11)
        => Catalog.Seat.FromPersistence(id, 1, sectionId, row, number, SeatStatus.Held, Guid.NewGuid(), heldUntil);

    public static Seat SoldSeat(int id, int row, int number, int sectionId = 11)
        => Catalog.Seat.FromPersistence(id, 1, sectionId, row, number, SeatStatus.Sold, Guid.NewGuid(), null);

    public static SeatHoldItem Item(int seatId, int row, int number, Section? section = null)
    {
        var s = section ?? Section();
        return new SeatHoldItem(Seat(seatId, row, number, s.Id), s);
    }

    public static SeatHold Hold(Guid? id = null, Guid? buyerId = null, int seatCount = 2,
                                DateTimeOffset? now = null, TimeSpan? holdFor = null)
    {
        var items = Enumerable.Range(1, seatCount)
            .Select(i => Item(seatId: 1100 + i, row: 1, number: i))
            .ToList();

        return new SeatHold(id ?? Guid.NewGuid(), buyerId ?? Guid.NewGuid(), performanceId: 1,
                            items, now ?? Now, holdFor ?? HoldFor);
    }
}
