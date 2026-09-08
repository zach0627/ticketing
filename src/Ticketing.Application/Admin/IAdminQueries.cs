using Ticketing.Application.Admin.Dtos;
using Ticketing.Application.Common;

namespace Ticketing.Application.Admin;

/// <summary>
/// 後台的唯讀查詢。跟 <c>IAdminDao</c> 分開：
/// **DAO 改資料、Queries 讀資料**，兩邊不重複放同一套統計（設計文件 14 第 2 節）。
/// </summary>
public interface IAdminQueries
{
    Task<DashboardDto> GetDashboardAsync(DateTimeOffset now, CancellationToken ct);

    Task<PagedResult<AdminOrderDto>> GetOrdersAsync(int? performanceId, int page, int pageSize,
                                                    CancellationToken ct);
}
