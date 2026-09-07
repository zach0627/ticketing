using Ticketing.Domain.Catalog;

namespace Ticketing.Application.Catalog.Dtos;

/// <summary>
/// 活動詳情。<c>MaxTicketsPerBuyer</c> 與 <c>AllowsContiguousAllocation</c> 來自
/// <c>PurchasePolicy</c>——**上限不存資料表**，由 Domain 依類別決定（設計文件 04 第 2.2 節）。
/// </summary>
public sealed record EventDetailDto(
    int Id,
    string Code,
    EventCategory Category,
    string Title,
    string Performer,
    string Genre,
    string Description,
    string ImagePath,
    PerformanceSummaryDto Performance,
    IReadOnlyList<SectionDto> Sections,
    int MaxTicketsPerBuyer,
    bool AllowsContiguousAllocation,
    string Currency,
    DateTimeOffset ServerNowUtc);
