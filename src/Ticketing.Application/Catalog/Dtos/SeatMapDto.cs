using Ticketing.Domain.Catalog;

namespace Ticketing.Application.Catalog.Dtos;

/// <summary>
/// 選位頁一次拿齊需要的東西：價格、政策、售票狀態、座位。
/// 這樣直接開網址或重新整理都能運作，不依賴前一頁留在記憶體的資料（設計文件 13 第 2 節）。
/// </summary>
public sealed record SeatMapDto(
    int PerformanceId,
    string EventCode,
    string EventTitle,
    EventCategory Category,
    int MaxTicketsPerBuyer,
    bool AllowsContiguousAllocation,
    SalesStatus SalesStatus,
    DateTimeOffset ServerNowUtc,
    IReadOnlyList<SectionDto> Sections,
    IReadOnlyList<SeatDto> Seats);
