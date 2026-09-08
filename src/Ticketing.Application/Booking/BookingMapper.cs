using Riok.Mapperly.Abstractions;
using Ticketing.Application.Booking.Dtos;
using Ticketing.Domain.Booking;

namespace Ticketing.Application.Booking;

/// <summary>
/// <see cref="SeatHold"/> → <see cref="HoldDto"/>。
///
/// 外層是手寫的：<c>HoldDto</c> 需要三個 Domain 物件裡沒有的東西——
/// 伺服器現在時間、對應的訂單 id、幣別。Mapperly 負責的是**逐欄位重複的那部分**
/// （明細清單），那正是它擅長也最容易寫錯的地方。
/// </summary>
[Mapper]
public static partial class BookingMapper
{
    public static HoldDto ToDto(SeatHold hold, DateTimeOffset now, Guid? orderId, string currency)
    {
        ArgumentNullException.ThrowIfNull(hold);

        return new HoldDto(
            hold.Id,
            hold.PerformanceId,
            hold.StatusAt(now),          // 資料庫 Active 但已過期 → 對外是 Expired
            orderId,
            now,
            hold.ExpiresAtUtc,
            currency,
            hold.TotalAmount,
            ToItemDtos(hold.Items));
    }

    /// <summary>Mapperly 產生：<c>SeatHoldItem</c> 的五個欄位全部要有交代，少一個就是 RMG020 編譯錯誤。</summary>
    private static partial IReadOnlyList<HoldItemDto> ToItemDtos(IReadOnlyList<SeatHoldItem> items);
}
