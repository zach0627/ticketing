using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Ticketing.Api.Tests.Infrastructure;

namespace Ticketing.Api.Tests;

[Collection(nameof(SqlServerCollection))]
public class EventPresentationMigrationTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Refresh_changes_only_catalog_presentation_and_preserves_existing_purchases()
    {
        await using var database = await fixture.SeededDatabaseAsync();
        await using var db = database.CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260907141417_InitialCreate");
        Assert.Equal("夜航之後", await db.Events.Where(e => e.Code == "C01").Select(e => e.Title).SingleAsync());

        var buyers = await database.CreateBuyersAsync(2);
        using (var factory = new TicketingApiFactory(database.ConnectionString))
        {
            using var buyer = factory.ClientFor(buyers[0]);
            var response = await buyer.PostHoldAsync(1, BookingTestHelpers.Manual(11, 1101), BookingTestHelpers.NewKey());
            response.EnsureSuccessStatusCode();
            var holdId = await response.HoldIdAsync();
            var order = await buyer.CheckoutAsync(holdId, "Succeeded", BookingTestHelpers.NewKey());
            order.EnsureSuccessStatusCode();

            using var second = factory.ClientFor(buyers[1]);
            var active = await second.PostHoldAsync(1, BookingTestHelpers.Manual(11, 1102), BookingTestHelpers.NewKey());
            active.EnsureSuccessStatusCode();
        }

        var tables = new Dictionary<string, string>
        {
            ["AppUsers"] = "Id", ["Performances"] = "Id", ["Sections"] = "Id",
            ["Seats"] = "Id", ["SeatHolds"] = "Id", ["SeatHoldItems"] = "HoldId, SeatId",
            ["Orders"] = "Id", ["OrderItems"] = "OrderId, SeatId",
            ["IdempotencyRecords"] = "BuyerId, [Key]"
        };
        var before = new Dictionary<string, string>();
        foreach (var (table, order) in tables)
            before[table] = await SnapshotAsync(table, order);
        var catalogIdentity = await database.ScalarAsync<string>(
            "SELECT (SELECT Id, Code, Category, IsPublished FROM Events ORDER BY Id FOR JSON PATH);");

        await migrator.MigrateAsync();

        foreach (var (table, order) in tables)
            Assert.Equal(before[table], await SnapshotAsync(table, order));
        Assert.Equal(catalogIdentity, await database.ScalarAsync<string>(
            "SELECT (SELECT Id, Code, Category, IsPublished FROM Events ORDER BY Id FOR JSON PATH);"));
        Assert.Equal(15, await db.Events.CountAsync(e => e.ImagePath.EndsWith("-v2.webp")));
        Assert.Equal(2, await db.Events.CountAsync(e => e.Genre == "K-pop"));
        Assert.Equal("LUMINA《BLOOM》台北演唱會", await db.Events.Where(e => e.Code == "C01").Select(e => e.Title).SingleAsync());
        Assert.Equal("Sold", await database.SeatStatusAsync(1101));
        Assert.Equal("Held", await database.SeatStatusAsync(1102));

        // 回滾只還原展示資訊，也不能改動已售與保留中的座位。
        await migrator.MigrateAsync("20260907141417_InitialCreate");
        Assert.Equal("夜航之後", await db.Events.Where(e => e.Code == "C01").Select(e => e.Title).SingleAsync());
        Assert.Equal(before["Orders"], await SnapshotAsync("Orders", "Id"));
        Assert.Equal(before["Seats"], await SnapshotAsync("Seats", "Id"));

        Task<string> SnapshotAsync(string table, string order)
            => database.ScalarAsync<string>($"SELECT (SELECT * FROM [{table}] ORDER BY {order} FOR JSON PATH, INCLUDE_NULL_VALUES);");
    }
}
