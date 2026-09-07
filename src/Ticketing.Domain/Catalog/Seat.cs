using Ticketing.Domain.Common;

namespace Ticketing.Domain.Catalog;

/// <summary>
/// 場次裡的一個編號座位。
/// **狀態轉換不在這裡**：保留、售出、釋放都是 <c>SeatRepository</c> 的條件 UPDATE，
/// 因為那是跨列的競態，必須由資料庫原子完成（設計文件 04 第 2.4 節、06 第 4 節）。
/// 這個類別只回答一個問題：此刻它算不算可用。
/// </summary>
public sealed class Seat
{
    public int Id { get; private set; }
    public int PerformanceId { get; private set; }
    public int SectionId { get; private set; }
    public int RowNumber { get; private set; }
    public int SeatNumber { get; private set; }
    public SeatStatus Status { get; private set; }
    public Guid? HoldId { get; private set; }
    public DateTimeOffset? HeldUntilUtc { get; private set; }

    private Seat() { }                                      // EF Core 用

    public Seat(int id, int performanceId, int sectionId, int rowNumber, int seatNumber)
    {
        if (id <= 0 || performanceId <= 0 || sectionId <= 0 || rowNumber <= 0 || seatNumber <= 0)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "座位的識別與排號必須大於 0");

        Id = id;
        PerformanceId = performanceId;
        SectionId = sectionId;
        RowNumber = rowNumber;
        SeatNumber = seatNumber;
        Status = SeatStatus.Available;
    }

    /// <summary>
    /// 重建一個已經處於 Held／Sold 狀態的座位。**只給測試用**——
    /// 正式路徑上這些狀態一律由 <c>SeatRepository</c> 的條件 UPDATE 產生，
    /// 不提供公開的狀態轉換方法，避免有人先查再無條件寫（設計文件 06 第 4 節）。
    /// </summary>
    internal static Seat FromPersistence(int id, int performanceId, int sectionId, int rowNumber,
                                         int seatNumber, SeatStatus status,
                                         Guid? holdId, DateTimeOffset? heldUntilUtc)
    {
        var seat = new Seat(id, performanceId, sectionId, rowNumber, seatNumber);
        seat.Status = status;
        seat.HoldId = holdId;
        seat.HeldUntilUtc = heldUntilUtc;
        return seat;
    }

    /// <summary>
    /// 過期的 Held 視同可用——**沒有背景清理程式**，正確性不依賴它。
    /// 同一條語意另外寫在座位圖投影與 <c>SeatRepository.TryHoldAsync</c> 的 WHERE，
    /// 三處必須一致，由整合測試 T08 保證。
    /// </summary>
    public bool IsAvailableAt(DateTimeOffset now)
        => Status == SeatStatus.Available
        || (Status == SeatStatus.Held && HeldUntilUtc is { } until && until <= now);
}
