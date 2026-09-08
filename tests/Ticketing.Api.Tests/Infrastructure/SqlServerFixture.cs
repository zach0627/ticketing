using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Ticketing.Infrastructure;
using Ticketing.Infrastructure.Persistence;

namespace Ticketing.Api.Tests.Infrastructure;

/// <summary>
/// 一個真的 SQL Server 容器，整個測試 collection 共用；**每個案例自己開一個空資料庫**，
/// 跑完刪掉（設計文件 10 第 1.2 節）。
///
/// 映像固定 digest，不用 <c>2022-latest</c>——latest 哪天更新，測試結果就不可重現。
/// 這個 digest 與本機開發容器相同。
///
/// ⚠️ 開發機是 arm64，這個 amd64 映像是模擬執行、Microsoft 官方未支援。
/// 權威結果以 CI 的 x86-64 runner 為準（設計文件 22 第 5 節）。
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string Image =
        "mcr.microsoft.com/mssql/server@sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89";

    // Testcontainers 4.15 起要求把映像傳進建構式（無參數版本已標為過時）；
    // MsSqlBuilder 自帶等待策略，不必再自訂。
    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    // xUnit 2.x 的 IAsyncLifetime 是 Task（ValueTask 是 v3）
    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// 建立一個唯一命名的空資料庫並套用真 migration。回傳它的連線字串。
    ///
    /// <paramref name="readCommittedSnapshot"/> 對應 T19：本機 SQL Server 預設 **OFF**，
    /// Azure SQL Database 預設 **ON**。兩者的「一般 SELECT 會不會被鎖住」完全不同，
    /// 所以併發協定要在兩種設定下各驗一次（設計文件 06 第 3.1 節）。
    /// </summary>
    public async Task<TestDatabase> CreateDatabaseAsync(bool readCommittedSnapshot = false,
                                                        CancellationToken ct = default)
    {
        var name = "t_" + Guid.NewGuid().ToString("N");

        await using (var admin = new SqlConnection(_container.GetConnectionString()))
        {
            await admin.OpenAsync(ct);
            await using var command = admin.CreateCommand();
            command.CommandText = readCommittedSnapshot
                ? $"CREATE DATABASE [{name}]; ALTER DATABASE [{name}] SET READ_COMMITTED_SNAPSHOT ON;"
                : $"CREATE DATABASE [{name}];";
            await command.ExecuteNonQueryAsync(ct);
        }

        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = name
        };
        var connectionString = builder.ConnectionString;

        var options = new DbContextOptionsBuilder<TicketingDbContext>()
            .UseSqlServer(connectionString).Options;
        await using (var db = new TicketingDbContext(options))
            await db.Database.MigrateAsync(ct);                 // 真 migration，不用 EnsureCreated

        return new TestDatabase(this, name, connectionString);
    }

    internal async Task DropDatabaseAsync(string name)
    {
        await using var admin = new SqlConnection(_container.GetConnectionString());
        await admin.OpenAsync();
        await using var command = admin.CreateCommand();
        command.CommandText =
            $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];";
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>一個案例專用的資料庫。Dispose 時刪除。</summary>
public sealed class TestDatabase(SqlServerFixture fixture, string name, string connectionString) : IAsyncDisposable
{
    public string ConnectionString { get; } = connectionString;

    public SqlConnection OpenConnection()
    {
        var connection = new SqlConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    public TicketingDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<TicketingDbContext>().UseSqlServer(ConnectionString).Options);

    /// <summary>
    /// 用真的 <c>AddInfrastructure()</c> 建容器——順便驗證註冊完整：
    /// <c>ValidateOnBuild</c> ＋ <c>ValidateScopes</c> 打開，漏註冊會在這裡就失敗。
    /// </summary>
    public ServiceProvider BuildServices(Action<Dictionary<string, string?>>? configure = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Ticketing"] = ConnectionString,
            ["Seed:Admin:Email"] = "admin@example.com",
            ["Seed:Admin:Password"] = Guid.NewGuid().ToString("N"),   // 測試自產，不沿用開發或雲端 secret
            ["Seed:PresetUsers:Enabled"] = "true",
            ["Seed:PresetUsers:Password"] = Guid.NewGuid().ToString("N")
        };
        configure?.Invoke(settings);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    public async ValueTask DisposeAsync() => await fixture.DropDatabaseAsync(name);
}

[CollectionDefinition(nameof(SqlServerCollection))]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
