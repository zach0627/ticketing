using System.Net;
using Ticketing.Api.Tests.Infrastructure;

namespace Ticketing.Api.Tests;

/// <summary>
/// T19、T20：**換掉環境假設**，同一組協定要照樣成立（設計文件 10 第 2.2 節）。
///
/// T19 換的是資料庫隔離設定：本機 SQL Server 預設 <c>READ_COMMITTED_SNAPSHOT OFF</c>、
/// Azure SQL Database 預設 <c>ON</c>，兩者「一般 SELECT 會不會被鎖住」完全不同。
/// 我們的協定用**明確的 gate** 而不是靠 SELECT 的鎖，所以兩種設定都該成立——這一組就是在驗這句話。
///
/// T20 換的是「同一個程式」這個假設。
/// </summary>
[Collection(nameof(SqlServerCollection))]
public class BookingIsolationAndProcessTests(SqlServerFixture fixture)
{
    [Theory] // T19
    [InlineData(false)]   // 本機 SQL Server 預設
    [InlineData(true)]    // Azure SQL Database 預設
    public async Task The_protocol_holds_under_both_read_committed_snapshot_settings(bool snapshot)
    {
        await using var db = await fixture.SeededDatabaseAsync(readCommittedSnapshot: snapshot);

        // 先確認這個資料庫真的是我們要的設定，不然這個測試等於什麼都沒測
        Assert.Equal(snapshot, await db.ScalarAsync<bool>(
            "SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME();"));

        var buyers = await db.CreateBuyersAsync(20);
        using var factory = new TicketingApiFactory(db.ConnectionString);

        var seat = Ids.ConcertSeat(1, 1);

        var responses = await Task.WhenAll(buyers.Select(async buyer =>
        {
            using var client = factory.ClientFor(buyer);
            return await client.PostHoldAsync(Ids.ConcertPerformance,
                BookingTestHelpers.Manual(Ids.ConcertSectionA, seat), BookingTestHelpers.NewKey());
        }));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await db.CountAsync("SeatHoldItems", "SeatId = @seat", ("@seat", seat)));
    }

    [Fact] // T20
    public async Task Correctness_does_not_depend_on_the_two_sides_sharing_a_process_state()
    {
        await using var db = await fixture.SeededDatabaseAsync();
        var buyers = await db.CreateBuyersAsync(10);

        // 兩個**互相獨立的 API host**：各自的 DI 容器、各自的 DbContext、各自的連線池。
        // 如果我們的防超賣靠的是程序內的 lock 或快取，這裡就會出現兩筆保留。
        //
        // 誠實地說：它們仍在同一個作業系統程序裡，不等於真的兩台機器。
        // 但它足以證明「正確性不依賴共用的記憶體狀態」，因為兩個 host 沒有共用任何東西。
        using var hostA = new TicketingApiFactory(db.ConnectionString);
        using var hostB = new TicketingApiFactory(db.ConnectionString);

        var seat = Ids.ConcertSeat(1, 1);

        var responses = await Task.WhenAll(buyers.Select(async (buyer, index) =>
        {
            var factory = index % 2 == 0 ? hostA : hostB;      // 請求平均分給兩個 host
            using var client = factory.ClientFor(buyer);
            return await client.PostHoldAsync(Ids.ConcertPerformance,
                BookingTestHelpers.Manual(Ids.ConcertSectionA, seat), BookingTestHelpers.NewKey());
        }));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, await db.CountAsync("SeatHoldItems", "SeatId = @seat", ("@seat", seat)));

        // 冪等也不能依賴程序內狀態：同一個 key 從 A 送、再從 B 送，必須拿到同一筆
        var buyer = (await db.CreateBuyersAsync(1))[0];
        var key = BookingTestHelpers.NewKey();
        var body = BookingTestHelpers.Manual(Ids.ConcertSectionA, Ids.ConcertSeat(2, 1));

        using var fromA = hostA.ClientFor(buyer);
        using var fromB = hostB.ClientFor(buyer);

        var first = await fromA.PostHoldAsync(Ids.ConcertPerformance, body, key);
        var second = await fromB.PostHoldAsync(Ids.ConcertPerformance, body, key);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(await first.HoldIdAsync(), await second.HoldIdAsync());
    }
}
