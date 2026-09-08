using Ticketing.Domain.Catalog;

namespace Ticketing.Application.Admin.Dtos;

/// <summary>後台場次列表的一列。<c>salesStatus</c> 依同一次業務 <c>now</c> 算出來。</summary>
public sealed record AdminPerformanceDto(
    int Id,
    string EventCode,
    string EventTitle,
    DateTimeOffset StartsAtUtc,
    bool IsSalesPaused,
    SalesStatus SalesStatus);
