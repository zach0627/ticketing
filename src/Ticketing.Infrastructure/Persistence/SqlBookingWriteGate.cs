using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;   // GetDbTransaction()：命令必須掛在目前的 EF 交易上
using Ticketing.Application.Abstractions;

namespace Ticketing.Infrastructure.Persistence;

/// <summary>
/// 在**目前這個 EF 交易**裡對固定資料列取鎖。
///
/// 三個 SQL 都明確指定同一組 clustered 主鍵索引：如果一條走 covering index、
/// 另一條走 clustered key，鎖到的可能不是同一個資源，協定就不成立
/// （設計文件 06 第 3.1 節）。
///
/// 用原生 <see cref="IDbCommand"/> 而不是 LINQ，因為需要 table hint；
/// 命令必須掛在目前的交易上，不另開連線。
/// </summary>
public sealed class SqlBookingWriteGate(TicketingDbContext db) : IBookingWriteGate
{
    private const string PerformanceRead =
        "SELECT Id FROM dbo.Performances WITH (HOLDLOCK, INDEX(PK_Performances)) WHERE Id = @id;";

    private const string PerformanceWrite =
        "SELECT Id FROM dbo.Performances WITH (XLOCK, HOLDLOCK, INDEX(PK_Performances)) WHERE Id = @id;";

    private const string Buyer =
        "SELECT Id FROM dbo.AppUsers WITH (UPDLOCK, HOLDLOCK, INDEX(PK_AppUsers)) WHERE Id = @id;";

    public Task<bool> EnterPerformanceReadAsync(int performanceId, CancellationToken ct)
        => ExistsAsync(PerformanceRead, performanceId, ct);

    public Task<bool> EnterPerformanceWriteAsync(int performanceId, CancellationToken ct)
        => ExistsAsync(PerformanceWrite, performanceId, ct);

    public Task<bool> EnterBuyerAsync(Guid buyerId, CancellationToken ct)
        => ExistsAsync(Buyer, buyerId, ct);

    /// <summary>
    /// 全域重置：先取得場次 ID 清單，**在 C# 排序後依序逐筆**取排他鎖。
    /// 不能用一條 <c>SELECT … ORDER BY</c> 就宣稱鎖一定依該順序取得。
    /// </summary>
    public async Task EnterAllPerformancesWriteAsync(CancellationToken ct)
    {
        var ids = await db.Performances.AsNoTracking()
                          .Select(p => p.Id)
                          .ToListAsync(ct);
        ids.Sort();

        foreach (var id in ids)
            await ExistsAsync(PerformanceWrite, id, ct);
    }

    private async Task<bool> ExistsAsync(string sql, object id, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await db.Database.OpenConnectionAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(ct);
        return result is not null and not DBNull;
    }
}
