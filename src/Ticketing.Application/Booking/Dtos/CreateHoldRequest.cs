using System.ComponentModel.DataAnnotations;

namespace Ticketing.Application.Booking.Dtos;

/// <summary>
/// 建立保留。**不接受 <c>buyerId</c>、<c>price</c> 或 <c>role</c>**——
/// DTO 裡根本沒有這些欄位，多送會被忽略（設計文件 13 第 2 節）。
/// 買家來自 JWT，價格來自資料庫的票區。
///
/// 這裡的 attribute 只做「形狀」檢查（型別、範圍、必填）；
/// 「這個活動能不能連號」「張數有沒有超過該類別上限」要看 <c>PurchasePolicy</c>，
/// 由 <c>BookingService</c> 判斷。
/// </summary>
public sealed record CreateHoldRequest
{
    [Range(1, int.MaxValue)]
    public int SectionId { get; init; }

    /// <summary>1～6：上限的絕對值。實際上限依活動類別（4 或 6）再檢查一次。</summary>
    [Range(1, 6)]
    public int Quantity { get; init; }

    [Required]
    public SelectionMode SelectionMode { get; init; }

    /// <summary>Manual 時必填且數量等於 <see cref="Quantity"/>；Contiguous 時必須是空陣列。</summary>
    [MaxLength(6)]
    public IReadOnlyList<int> SeatIds { get; init; } = [];
}
