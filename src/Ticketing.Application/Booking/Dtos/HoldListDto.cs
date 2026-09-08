namespace Ticketing.Application.Booking.Dtos;

/// <summary>
/// <c>GET /me/holds?performanceId=</c> 的回應。
/// 0 或 1 筆——同一場次每人只能有一個有效保留（設計文件 02 第 1 節）。
/// 用物件包起來而不是直接回陣列，之後要加欄位才不會是破壞性變更。
/// </summary>
public sealed record HoldListDto(IReadOnlyList<HoldDto> Items);
