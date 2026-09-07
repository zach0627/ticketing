namespace Ticketing.Domain.Catalog;

/// <summary>
/// 場次此刻的售票狀態。**不存資料表**——它會自己過期，每次讀取時用時間算
/// （設計文件 04 第 2.4 節）。
/// </summary>
public enum SalesStatus
{
    NotYetOnSale,
    OnSale,
    SalesClosed,
    Paused
}
