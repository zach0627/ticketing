using Ticketing.Application.Booking;
using Ticketing.Application.Booking.Dtos;
using Ticketing.Application.Orders;
using Ticketing.Application.Orders.Dtos;
using Ticketing.Domain.Booking;
using Ticketing.Domain.Orders;

namespace Ticketing.Application.Tests;

/// <summary>
/// M01-Booking／M01-Order：實際跑一次對應，**逐欄位比對值**。
///
/// 每個欄位刻意給不同的值：如果只檢查「有值」，把 <c>RowNumber</c> 與 <c>SeatNumber</c>
/// 對調的 bug 一樣會通過（設計文件 10 第 1.1 節）。
/// </summary>
public class BookingAndOrderMapperTests
{
    private static readonly DateTimeOffset Now = BookingTestContext.Now;

    [Fact]
    public void A_hold_maps_every_field_including_the_two_time_bases()
    {
        var hold = BookingTestContext.ActiveHold(Now.AddMinutes(-2),
            BookingTestContext.SeatOf(1101, 3, 7),        // 排 3、號 7：兩個值不同才測得出對調
            BookingTestContext.SeatOf(1102, 4, 9));

        var dto = BookingMapper.ToDto(hold, Now, orderId: null, "TWD");

        Assert.Equal(hold.Id, dto.Id);
        Assert.Equal(1, dto.PerformanceId);
        Assert.Equal(HoldStatus.Active, dto.Status);
        Assert.Null(dto.OrderId);
        Assert.Equal(Now, dto.ServerNowUtc);                     // 前端倒數的基準點
        Assert.Equal(Now.AddMinutes(3), dto.ExpiresAtUtc);       // 建立時 -2 分，保留 5 分
        Assert.Equal("TWD", dto.Currency);
        Assert.Equal(4200m, dto.TotalAmount);

        var first = dto.Items[0];
        Assert.Equal(1101, first.SeatId);
        Assert.Equal("A", first.SectionCode);
        Assert.Equal(3, first.RowNumber);
        Assert.Equal(7, first.SeatNumber);
        Assert.Equal(2100m, first.UnitPrice);
    }

    [Fact]
    public void A_hold_that_is_active_in_the_database_but_past_its_time_shows_as_expired()
    {
        var hold = BookingTestContext.ActiveHold(Now.AddMinutes(-10), BookingTestContext.SeatOf(1101, 1, 1));

        Assert.Equal(HoldStatus.Active, hold.Status);                                  // 資料庫欄位
        Assert.Equal(HoldStatus.Expired, BookingMapper.ToDto(hold, Now, null, "TWD").Status);   // 對外
    }

    [Fact]
    public void HoldItemDto_carries_the_snapshot_and_nothing_else()
    {
        // 保留明細不該外洩 holdId 或買家；它就是「哪一席、多少錢」
        var names = typeof(HoldItemDto).GetProperties().Select(p => p.Name)
            .Where(name => name != "EqualityContract").Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["RowNumber", "SeatId", "SeatNumber", "SectionCode", "UnitPrice"], names);
    }

    [Fact]
    public void An_order_maps_the_snapshot_fields_onto_their_contract_names()
    {
        var hold = BookingTestContext.ActiveHold(Now.AddMinutes(-1),
            BookingTestContext.SeatOf(1102, 4, 9),
            BookingTestContext.SeatOf(1101, 3, 7));
        var performance = BookingTestContext.PerformanceOf(BookingTestContext.ConcertEvent());
        var order = Order.FromHold(Guid.NewGuid(), hold, performance, Now);

        var dto = OrderMapper.ToDto(order);

        Assert.Equal(order.Id, dto.Id);
        Assert.Equal(hold.Id, dto.HoldId);
        Assert.Equal("夜藍之後", dto.EventTitle);                    // EventTitleSnapshot → eventTitle
        Assert.Equal(performance.StartsAtUtc, dto.StartsAtUtc);      // StartsAtSnapshot → startsAtUtc
        Assert.Equal("TWD", dto.Currency);
        Assert.Equal(4200m, dto.TotalAmount);
        Assert.Equal(Now, dto.CreatedAtUtc);

        // 票號序號按 SeatId 升序，所以 1101 排第一——即使保留裡是 1102 先加入的
        Assert.Equal(2, dto.Items.Count);
        Assert.Equal(3, dto.Items[0].RowNumber);
        Assert.Equal(7, dto.Items[0].SeatNumber);
        Assert.EndsWith("-01", dto.Items[0].TicketCode, StringComparison.Ordinal);
        Assert.EndsWith("-02", dto.Items[1].TicketCode, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderDto_does_not_expose_the_buyer_or_the_performance()
    {
        var names = typeof(OrderDto).GetProperties().Select(p => p.Name)
            .Where(name => name != "EqualityContract").Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(
            ["CreatedAtUtc", "Currency", "EventTitle", "HoldId", "Id", "Items", "StartsAtUtc", "TotalAmount"],
            names);
    }

    [Fact]
    public void OrderItemDto_does_not_expose_the_internal_seat_id()
    {
        // 票券上有區排號就夠了；SeatId 是我們的內部識別，沒有理由給出去
        var names = typeof(OrderItemDto).GetProperties().Select(p => p.Name)
            .Where(name => name != "EqualityContract").Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["RowNumber", "SeatNumber", "SectionCode", "TicketCode", "UnitPrice"], names);
    }
}
