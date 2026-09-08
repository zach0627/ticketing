using Microsoft.EntityFrameworkCore;
using Ticketing.Application.Admin;
using Ticketing.Application.Admin.Dtos;
using Ticketing.Application.Common;
using Ticketing.Domain.Booking;
using Ticketing.Domain.Catalog;
using Ticketing.Domain.Orders;
using Ticketing.Domain.Users;

namespace Ticketing.Infrastructure.Persistence.Queries;

/// <summary>後台的唯讀查詢：投影成 DTO，不經過 Domain（ADR-5）。</summary>
public sealed class AdminQueries(TicketingDbContext db) : IAdminQueries
{
    public async Task<DashboardDto> GetDashboardAsync(DateTimeOffset now, CancellationToken ct)
    {
        // 「有效保留」是**還沒到期**的，不是資料庫裡 Status='Active' 的筆數——
        // 沒有背景清理程式，過期的那些還留在 Active（設計文件 04 第 5 節）
        var activeHolds = await db.SeatHolds.AsNoTracking()
            .CountAsync(h => h.Status == HoldStatus.Active && h.ExpiresAtUtc > now, ct);

        var soldSeats = await db.Seats.AsNoTracking().CountAsync(s => s.Status == SeatStatus.Sold, ct);
        var orders = await db.Orders.AsNoTracking().CountAsync(ct);

        var performances = await db.Performances.AsNoTracking()
            .OrderBy(p => p.StartsAtUtc).ThenBy(p => p.Id)
            .Select(p => new AdminPerformanceDto(
                p.Id,
                p.Event.Code,
                p.Event.Title,
                p.StartsAtUtc,
                p.IsSalesPaused,
                // 售票狀態的規則只有一份：Domain 的靜態純函式，投影直接呼叫它
                Performance.SalesStatusAt(p.IsSalesPaused, p.SalesOpensAtUtc, p.SalesClosesAtUtc, now)))
            .ToListAsync(ct);

        return new DashboardDto(activeHolds, soldSeats, orders, now, performances);
    }

    public async Task<PagedResult<AdminOrderDto>> GetOrdersAsync(int? performanceId, int page, int pageSize,
                                                                 CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking();
        if (performanceId is { } id) query = query.Where(o => o.PerformanceId == id);

        var total = await query.CountAsync(ct);

        // 買家顯示名要 join AppUsers。**只取 DisplayName**——後台不需要 Email
        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(db.Set<AppUser>().AsNoTracking(), o => o.BuyerId, u => u.Id, (o, u) => new AdminOrderDto(
                o.Id,
                o.HoldId,
                o.PerformanceId,
                o.EventTitleSnapshot,
                o.StartsAtSnapshot,
                u.DisplayName,
                EF.Property<IReadOnlyList<OrderItem>>(o, "_items").Count,
                o.TotalAmount,
                o.Currency,
                o.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<AdminOrderDto>(items, total);
    }
}
