using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ticketing.Infrastructure.Persistence;

namespace Ticketing.Api.Tests.Infrastructure;

/// <summary>
/// 用真的 <c>Program</c> 啟動 API，只把資料庫換成測試專用的那一個。
///
/// **刻意不換掉任何其他東西**：Controller、Queries、對應器、錯誤處理、JSON 設定
/// 全部是正式的那一套，測到的才是真的行為（設計文件 10 第 1.2 節）。
/// </summary>
public sealed class CatalogApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // 先移除原本的 DbContext 與它的 options，避免同時連到本機開發庫
            services.RemoveAll<DbContextOptions<TicketingDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<TicketingDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<TicketingDbContext>>();

            services.AddDbContext<TicketingDbContext>(o => o.UseSqlServer(connectionString,
                sql => sql.CommandTimeout(10).EnableRetryOnFailure(2, TimeSpan.FromSeconds(2), null)));
        });
    }
}
