namespace Ticketing.Application.Booking.Dtos;

/// <summary>
/// 保留裡的一席。這些值是**保留當下的快照**（ADR-8），
/// 活動之後改價不影響已經按下去的人看到的金額。
/// </summary>
public sealed record HoldItemDto(int SeatId, string SectionCode, int RowNumber, int SeatNumber, decimal UnitPrice);
