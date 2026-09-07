using Ticketing.Domain.Catalog;

namespace Ticketing.Application.Catalog.Dtos;

public sealed record PerformanceSummaryDto(
    int Id,
    string City,
    string Venue,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset SalesOpensAtUtc,
    DateTimeOffset SalesClosesAtUtc,
    int DurationMinutes,
    bool IsSalesPaused,
    SalesStatus SalesStatus);
