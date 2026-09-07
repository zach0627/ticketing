namespace Ticketing.Domain.Catalog;

/// <summary>
/// 座位狀態。與 HoldId／HeldUntilUtc 的合法組合由資料庫 CHECK 保證（設計文件 04 第 5 節）：
/// Available → 兩者皆 NULL；Held → 兩者皆非 NULL；Sold → HoldId 非 NULL、HeldUntilUtc 為 NULL。
/// </summary>
public enum SeatStatus
{
    Available,
    Held,
    Sold
}
