using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Api.Tests.Infrastructure;
using Ticketing.Domain.Catalog;
using Ticketing.Domain.Users;
using Ticketing.Infrastructure.Persistence.Seeding;

namespace Ticketing.Api.Tests;

/// <summary>
/// T13：空資料庫跑 migration ＋ seed 兩次。
/// 第二次必須是 AlreadySeeded；15／12／3 場、2,880 席、ID 無重複（設計文件 10 第 2 節）。
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class SeedTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task T13_seeding_an_empty_database_twice_is_idempotent()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var services = database.BuildServices();

        // ── 第一次 ──
        var first = await RunSeedAsync(services);
        Assert.Equal(SeedOutcome.Seeded, first.Outcome);
        Assert.Equal(15, first.Events);
        Assert.Equal(15, first.Performances);
        Assert.Equal(45, first.Sections);
        Assert.Equal(2880, first.Seats);
        Assert.Equal(4, first.Users);              // 管理者 ＋ 三個預建帳號

        // ── 第二次：不得重複建立任何東西 ──
        var second = await RunSeedAsync(services);
        Assert.Equal(SeedOutcome.AlreadySeeded, second.Outcome);
        Assert.Equal(0, second.Events);
        Assert.Equal(0, second.Seats);
        Assert.Equal(0, second.Users);

        await using var db = database.CreateDbContext();

        Assert.Equal(15, await db.Events.CountAsync());
        Assert.Equal(12, await db.Events.CountAsync(e => e.Category == EventCategory.Concert));
        Assert.Equal(3, await db.Events.CountAsync(e => e.Category == EventCategory.Sport));
        Assert.Equal(45, await db.Sections.CountAsync());
        Assert.Equal(2880, await db.Seats.CountAsync());
        Assert.Equal(4, await db.AppUsers.CountAsync());
        Assert.Equal(1, await db.AppUsers.CountAsync(u => u.Role == UserRole.Admin));
    }

    [Fact]
    public async Task T13_seat_ids_follow_the_documented_formula_and_are_unique()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var services = database.BuildServices();
        await RunSeedAsync(services);

        await using var db = database.CreateDbContext();

        var ids = await db.Seats.Select(s => s.Id).ToListAsync();
        Assert.Equal(2880, ids.Count);
        Assert.Equal(2880, ids.Distinct().Count());

        // performanceId × 1000 + 區index × 100 + (排−1) × 每排席數 + 座號
        var c01 = await db.Seats.SingleAsync(s => s.Id == 1101);
        Assert.Equal((1, 11, 1, 1), (c01.PerformanceId, c01.SectionId, c01.RowNumber, c01.SeatNumber));

        var s01 = await db.Seats.SingleAsync(s => s.Id == 13101);
        Assert.Equal((13, 131, 1, 1), (s01.PerformanceId, s01.SectionId, s01.RowNumber, s01.SeatNumber));

        // 演唱會每場 180 席（5×12×3 區）、運動賽事 240 席（5×16×3 區）
        var perSeatCounts = await db.Seats.GroupBy(s => s.PerformanceId)
            .Select(g => new { PerformanceId = g.Key, Count = g.Count() })
            .ToListAsync();
        Assert.All(perSeatCounts.Where(x => x.PerformanceId <= 12), x => Assert.Equal(180, x.Count));
        Assert.All(perSeatCounts.Where(x => x.PerformanceId >= 13), x => Assert.Equal(240, x.Count));
    }

    [Fact]
    public async Task T13_every_seat_starts_available_and_every_sales_window_is_valid()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var services = database.BuildServices();
        await RunSeedAsync(services);

        await using var db = database.CreateDbContext();

        Assert.Equal(2880, await db.Seats.CountAsync(s => s.Status == SeatStatus.Available));
        Assert.Equal(0, await db.Seats.CountAsync(s => s.HoldId != null));
        Assert.Equal(15, await db.Performances.CountAsync(
            p => p.SalesOpensAtUtc < p.SalesClosesAtUtc && p.SalesClosesAtUtc < p.StartsAtUtc));
    }

    [Fact]
    public async Task Seed_fails_before_writing_anything_when_a_required_setting_is_missing()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var services = database.BuildServices(s => s["Seed:Admin:Password"] = null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => RunSeedAsync(services));

        Assert.Contains("Seed:Admin:Password", ex.Message, StringComparison.Ordinal);

        await using var db = database.CreateDbContext();
        Assert.Equal(0, await db.Events.CountAsync());          // Preflight 在寫入之前就擋下
    }

    private static async Task<SeedResult> RunSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();               // SeedRunner 是 Scoped
        return await scope.ServiceProvider.GetRequiredService<SeedRunner>().RunAsync(CancellationToken.None);
    }
}
