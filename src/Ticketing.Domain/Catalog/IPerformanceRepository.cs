namespace Ticketing.Domain.Catalog;

public interface IPerformanceRepository
{
    /// <summary>含 Event（PurchasePolicy 需要 Category）；未上架回 null。</summary>
    Task<Performance?> GetPublishedAsync(int performanceId, CancellationToken ct);

    /// <summary>管理者用，不看上架狀態。</summary>
    Task<Performance?> GetAsync(int performanceId, CancellationToken ct);

    Task<Section?> GetSectionAsync(int performanceId, int sectionId, CancellationToken ct);
}
