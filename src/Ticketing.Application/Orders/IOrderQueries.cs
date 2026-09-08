using Ticketing.Application.Common;
using Ticketing.Application.Orders.Dtos;

namespace Ticketing.Application.Orders;

/// <summary>
/// 訂單的**唯讀**查詢，直接投影成 DTO，不經過 Domain（ADR-5）。
///
/// 每個方法都收 <c>buyerId</c> 並放進 WHERE：
/// 「查得到才判斷有沒有權限」跟「本來就只查得到自己的」是兩種安全等級，
/// 這裡是後者（設計文件 05 第 4.3 節）。
/// </summary>
public interface IOrderQueries
{
    Task<PagedResult<OrderSummaryDto>> GetMyOrdersAsync(Guid buyerId, int page, int pageSize,
                                                        CancellationToken ct);

    Task<OrderDto?> GetMyOrderAsync(Guid buyerId, Guid orderId, CancellationToken ct);
}
