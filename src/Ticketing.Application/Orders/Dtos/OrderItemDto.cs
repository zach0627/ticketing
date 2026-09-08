namespace Ticketing.Application.Orders.Dtos;

/// <summary>
/// 一張票。<paramref name="TicketCode"/> 是可讀識別碼，
/// **不是入場憑證，也不是安全秘密**（設計文件 04 第 8 節）。
/// </summary>
public sealed record OrderItemDto(string SectionCode, int RowNumber, int SeatNumber,
                                  decimal UnitPrice, string TicketCode);
