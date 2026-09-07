using Microsoft.EntityFrameworkCore;
using Ticketing.Domain.Booking;
using Ticketing.Domain.Catalog;
using Ticketing.Domain.Orders;
using Ticketing.Domain.Users;
using Ticketing.Infrastructure.Persistence.Entities;

namespace Ticketing.Infrastructure.Persistence;

/// <summary>
/// 所有對應設定寫在 <c>Configurations/</c>，Domain 類別不掛任何 EF attribute
/// （設計文件 12 第 3 節的分層規則）。
/// </summary>
public sealed class TicketingDbContext(DbContextOptions<TicketingDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Performance> Performances => Set<Performance>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<SeatHold> SeatHolds => Set<SeatHold>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<AdminAudit> AdminAudits => Set<AdminAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketingDbContext).Assembly);
}
