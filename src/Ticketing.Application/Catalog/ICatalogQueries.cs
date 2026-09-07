using Ticketing.Application.Catalog.Dtos;
using Ticketing.Application.Common;
using Ticketing.Domain.Catalog;

namespace Ticketing.Application.Catalog;

/// <summary>
/// 唯讀查詢，直接回 DTO，**不經過 Domain**——讀寫分離（CQRS-lite，ADR-5）。
/// 列表與座位圖不需要載入聚合，投影一次到位。
/// 寫入走 <c>IXxxService</c> → Repository → Domain，那條路才有規則與交易。
///
/// 實作在 Infrastructure，用 <c>AsNoTracking</c> ＋ <c>Select</c>。
/// </summary>
public interface ICatalogQueries
{
    Task<PagedResult<EventCardDto>> GetEventCardsAsync(
        EventCategory? category, int page, int pageSize, DateTimeOffset now, CancellationToken ct);

    Task<EventDetailDto?> GetEventDetailAsync(string code, DateTimeOffset now, CancellationToken ct);

    Task<SeatMapDto?> GetSeatMapAsync(int performanceId, DateTimeOffset now, CancellationToken ct);
}
