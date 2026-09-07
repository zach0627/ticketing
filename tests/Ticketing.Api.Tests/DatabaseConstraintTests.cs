using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Api.Tests.Infrastructure;
using Ticketing.Infrastructure.Persistence.Seeding;

namespace Ticketing.Api.Tests;

/// <summary>
/// T24：直接用 SQL 造出違反約束的資料，確認資料庫**擋得住**。
///
/// 重點不只是「有沒有被拒絕」，而是「被**哪一條**約束拒絕」——
/// 用錯的理由被擋下是假通過（實作時就踩過：測同席二售卻先撞到 FK）。
/// 所以每個案例都斷言錯誤訊息含預期的約束名稱。
///
/// 這些是「就算程式有 bug 也寫不壞」的最後防線（設計文件 04 第 4 節）。
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class DatabaseConstraintTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task T24_the_same_seat_can_never_be_sold_twice()
        => await AssertRejectedAsync("UQ_OrderItems_Seat", """
            INSERT dbo.OrderItems(OrderId,SeatId,SectionCode,RowNumber,SeatNumber,UnitPrice,TicketCode)
            VALUES(@order2,1101,'A',1,1,2100,'DUP-01');
            """);

    [Fact]
    public async Task T24_a_hold_can_produce_at_most_one_order()
        => await AssertRejectedAsync("UQ_Orders_Hold", """
            INSERT dbo.Orders(Id,HoldId,BuyerId,PerformanceId,TotalAmount,Currency,EventTitleSnapshot,StartsAtSnapshot,CreatedAtUtc)
            VALUES(NEWID(),@hold1,@buyer1,1,2100,'TWD',N'x',@now,@now);
            """);

    [Fact]
    public async Task T24_a_buyer_cannot_hold_the_same_performance_twice()
        => await AssertRejectedAsync("UX_SeatHolds_ActiveBuyerPerformance", """
            INSERT dbo.SeatHolds(Id,BuyerId,PerformanceId,Status,CreatedAtUtc,ExpiresAtUtc,TotalAmount)
            VALUES(NEWID(),@buyer1,1,'Active',@now,DATEADD(minute,5,@now),2100);
            """);

    [Fact]
    public async Task T24_a_section_price_cannot_be_negative()
        => await AssertRejectedAsync("CK_Sections_Values", "UPDATE dbo.Sections SET Price = -1 WHERE Id = 11;");

    [Fact]
    public async Task T24_seat_status_and_hold_columns_must_stay_consistent()
        => await AssertRejectedAsync("CK_Seats_State", """
            UPDATE dbo.Seats SET Status='Available', HoldId=@hold1, HeldUntilUtc=NULL WHERE Id=1102;
            """);

    [Fact]
    public async Task T24_a_seat_cannot_be_held_by_a_hold_from_another_performance()
        => await AssertRejectedAsync("FK_Seats_HoldPerformance", """
            UPDATE dbo.Seats SET Status='Held', HoldId=@hold1, HeldUntilUtc=DATEADD(minute,5,@now) WHERE Id=2101;
            """);

    [Fact]
    public async Task T24_an_order_cannot_claim_a_hold_that_belongs_to_someone_else()
        => await AssertRejectedAsync("FK_Orders_HoldBuyerPerformance", """
            INSERT dbo.Orders(Id,HoldId,BuyerId,PerformanceId,TotalAmount,Currency,EventTitleSnapshot,StartsAtSnapshot,CreatedAtUtc)
            VALUES(NEWID(),@hold3,@buyer1,1,2100,'TWD',N'x',@now,@now);
            """);

    [Fact]
    public async Task T24_an_order_cannot_move_a_hold_to_another_performance()
        => await AssertRejectedAsync("FK_Orders_HoldBuyerPerformance", """
            INSERT dbo.Orders(Id,HoldId,BuyerId,PerformanceId,TotalAmount,Currency,EventTitleSnapshot,StartsAtSnapshot,CreatedAtUtc)
            VALUES(NEWID(),@hold3,@buyer2,2,2100,'TWD',N'x',@now,@now);
            """);

    [Fact]
    public async Task T24_an_admin_operation_id_cannot_be_reused()
        => await AssertRejectedAsync("UX_AdminAudits_OperationId", """
            INSERT dbo.AdminAudits(ActorId,Action,CreatedAtUtc,OperationId,RequestHash,ResultJson)
            VALUES(@buyer1,'Reset',@now,@operation,CONVERT(binary(32),REPLICATE('a',32)),'{}');
            """);

    [Fact]
    public async Task T24_audit_dedup_columns_must_all_be_present_or_all_absent()
        => await AssertRejectedAsync("CK_AdminAudits_Operation", """
            INSERT dbo.AdminAudits(ActorId,Action,CreatedAtUtc,OperationId,RequestHash,ResultJson)
            VALUES(@buyer1,'Reset',@now,NEWID(),NULL,NULL);
            """);

    /// <summary>
    /// 建好基準資料後執行 <paramref name="offendingSql"/>，斷言它被 <paramref name="expectedConstraint"/> 拒絕。
    /// </summary>
    private async Task AssertRejectedAsync(string expectedConstraint, string offendingSql)
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await using var services = database.BuildServices();

        using (var scope = services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SeedRunner>().RunAsync(CancellationToken.None);

        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var baseline = await SeedBaselineAsync(connection);

        await using var command = connection.CreateCommand();
        command.CommandText = offendingSql;
        baseline.Apply(command);

        var ex = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());

        Assert.Contains(expectedConstraint, ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 基準：買家1 在場次1 有 Active 保留 hold1 與已付款訂單（座位 1101）；
    /// 買家2 有已取消的 hold2（已開訂單，供同席二售用）與 hold3（無訂單，供複合 FK 用）。
    /// </summary>
    private static async Task<Baseline> SeedBaselineAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP 1 @b1 = Id FROM dbo.AppUsers WHERE Role='Customer' ORDER BY Email;
            SELECT TOP 1 @b2 = Id FROM dbo.AppUsers WHERE Role='Customer' AND Id <> @b1 ORDER BY Email DESC;

            INSERT dbo.SeatHolds(Id,BuyerId,PerformanceId,Status,CreatedAtUtc,ExpiresAtUtc,TotalAmount)
            VALUES(@h1,@b1,1,'Active',@now,DATEADD(minute,5,@now),2100),
                  (@h2,@b2,1,'Cancelled',@now,DATEADD(minute,5,@now),2100),
                  (@h3,@b2,1,'Cancelled',@now,DATEADD(minute,5,@now),2100);

            INSERT dbo.SeatHoldItems(HoldId,SeatId,SectionCode,RowNumber,SeatNumber,UnitPrice)
            VALUES(@h1,1101,'A',1,1,2100);

            INSERT dbo.Orders(Id,HoldId,BuyerId,PerformanceId,TotalAmount,Currency,EventTitleSnapshot,StartsAtSnapshot,CreatedAtUtc)
            VALUES(@o1,@h1,@b1,1,2100,'TWD',N'base',@now,@now),
                  (@o2,@h2,@b2,1,2100,'TWD',N'base2',@now,@now);

            INSERT dbo.OrderItems(OrderId,SeatId,SectionCode,RowNumber,SeatNumber,UnitPrice,TicketCode)
            VALUES(@o1,1101,'A',1,1,2100,'BASE-01');

            INSERT dbo.AdminAudits(ActorId,Action,CreatedAtUtc,OperationId,RequestHash,ResultJson)
            VALUES(@b1,'Reset',@now,@operation,CONVERT(binary(32),REPLICATE('a',32)),'{}');

            SELECT @b1 AS Buyer1, @b2 AS Buyer2;
            """;

        var baseline = new Baseline();
        baseline.Apply(command);
        command.Parameters["@b1"].Direction = System.Data.ParameterDirection.InputOutput;
        command.Parameters["@b2"].Direction = System.Data.ParameterDirection.InputOutput;

        await using (var reader = await command.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            baseline.Buyer1 = reader.GetGuid(0);
            baseline.Buyer2 = reader.GetGuid(1);
        }

        return baseline;
    }

    private sealed class Baseline
    {
        public Guid Buyer1 { get; set; }
        public Guid Buyer2 { get; set; }
        public Guid Hold1 { get; } = Guid.NewGuid();
        public Guid Hold2 { get; } = Guid.NewGuid();
        public Guid Hold3 { get; } = Guid.NewGuid();
        public Guid Order1 { get; } = Guid.NewGuid();
        public Guid Order2 { get; } = Guid.NewGuid();
        public Guid Operation { get; } = Guid.NewGuid();

        public void Apply(SqlCommand command)
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@b1", Buyer1);
            command.Parameters.AddWithValue("@b2", Buyer2);
            command.Parameters.AddWithValue("@buyer1", Buyer1);
            command.Parameters.AddWithValue("@buyer2", Buyer2);
            command.Parameters.AddWithValue("@h1", Hold1);
            command.Parameters.AddWithValue("@h2", Hold2);
            command.Parameters.AddWithValue("@h3", Hold3);
            command.Parameters.AddWithValue("@hold1", Hold1);
            command.Parameters.AddWithValue("@hold3", Hold3);
            command.Parameters.AddWithValue("@o1", Order1);
            command.Parameters.AddWithValue("@o2", Order2);
            command.Parameters.AddWithValue("@order2", Order2);
            command.Parameters.AddWithValue("@operation", Operation);
            command.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow);
        }
    }
}
