using Ticketing.Domain.Catalog;

namespace Ticketing.Application.Catalog.Dtos;

/// <summary>
/// 首頁卡片。<see cref="SalesStatus"/> 與 <see cref="ServerNowUtc"/> 需要當下時間，
/// 所以這個 DTO 由 <c>CatalogQueries</c> 手寫 <c>Select</c> 投影，不走 Mapperly
/// （設計文件 03 第 4.2 節）。
/// </summary>
public sealed record EventCardDto(
    int Id,
    string Code,
    EventCategory Category,
    string Title,
    string Performer,
    string ImagePath,
    string City,
    int PerformanceId,
    DateTimeOffset StartsAtUtc,
    decimal MinPrice,
    string Currency,
    SalesStatus SalesStatus,
    DateTimeOffset ServerNowUtc);
