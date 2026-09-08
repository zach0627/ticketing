namespace Ticketing.Application.Booking.Dtos;

/// <summary>
/// 怎麼決定要哪些座位。**沒有第三種**——未知值一律 400，
/// 不能用 <c>else</c> 把它當成連號（設計文件 13 第 1 節）。
/// </summary>
public enum SelectionMode
{
    /// <summary>使用者自己在座位圖上點的。</summary>
    Manual,

    /// <summary>系統在票區裡找第一段同排連續 N 席。只有運動賽事接受。</summary>
    Contiguous
}
