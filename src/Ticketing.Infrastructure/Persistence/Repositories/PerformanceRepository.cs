using Microsoft.EntityFrameworkCore;
using Ticketing.Domain.Catalog;

namespace Ticketing.Infrastructure.Persistence.Repositories;

public sealed class PerformanceRepository(TicketingDbContext db) : IPerformanceRepository
{
    /// <summary>含 Event（PurchasePolicy 需要 Category）；活動未上架視同不存在。</summary>
    public Task<Performance?> GetPublishedAsync(int performanceId, CancellationToken ct)
        => db.Performances
             .Include(p => p.Event)
             .FirstOrDefaultAsync(p => p.Id == performanceId && p.Event.IsPublished, ct);

    public Task<Performance?> GetAsync(int performanceId, CancellationToken ct)
        => db.Performances
             .Include(p => p.Event)
             .FirstOrDefaultAsync(p => p.Id == performanceId, ct);

    public Task<Section?> GetSectionAsync(int performanceId, int sectionId, CancellationToken ct)
        => db.Sections
             .FirstOrDefaultAsync(s => s.Id == sectionId && s.PerformanceId == performanceId, ct);
}
