using Ticketing.Domain.Booking;

namespace Ticketing.Application.Booking.Dtos;

/// <summary>
/// 一筆保留的對外樣子。
///
/// 兩個時間欄位要一起看：<paramref name="ServerNowUtc"/> 是**伺服器**的現在，
/// 前端用「expiresAt − serverNow」當倒數的起點，而不是用瀏覽器的時鐘減——
/// 使用者的電腦時間可能差好幾分鐘（設計文件 13 第 4 節）。
///
/// <paramref name="Status"/> 是 <c>StatusAt(now)</c> 的結果：資料庫還是 Active
/// 但已經過期的，這裡就會是 Expired。
/// </summary>
public sealed record HoldDto(
    Guid Id,
    int PerformanceId,
    HoldStatus Status,
    Guid? OrderId,
    DateTimeOffset ServerNowUtc,
    DateTimeOffset ExpiresAtUtc,
    string Currency,
    decimal TotalAmount,
    IReadOnlyList<HoldItemDto> Items);
