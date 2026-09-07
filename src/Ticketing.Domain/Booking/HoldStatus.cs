namespace Ticketing.Domain.Booking;

/// <summary>
/// 保留的狀態。注意 <see cref="Active"/> 是**資料庫欄位值**，可能已經過了到期時間卻還沒被整理；
/// 對外顯示要用 <c>SeatHold.StatusAt(now)</c>（設計文件 04 第 5 節）。
/// </summary>
public enum HoldStatus
{
    Active,
    Completed,
    Cancelled,
    Expired
}
