using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Catalog;
using Ticketing.Domain.Booking;
using Ticketing.Domain.Catalog;
using Ticketing.Domain.Common;
using Ticketing.Domain.Orders;
using Ticketing.Domain.Users;
using Ticketing.Application.Orders;
using Ticketing.Infrastructure.Persistence;
using Ticketing.Infrastructure.Persistence.Dao;
using Ticketing.Infrastructure.Persistence.Queries;
using Ticketing.Infrastructure.Persistence.Repositories;
using Ticketing.Infrastructure.Persistence.Seeding;
using Ticketing.Infrastructure.Security;

namespace Ticketing.Infrastructure;

/// <summary>
/// Infrastructure 的註冊集中在這裡，`Program.cs` 只呼叫一行——
/// Api 只在一個地方認識 Infrastructure（設計文件 03 第 4.3 節、ADR-4）。
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // ── 持久化：Scoped，一個請求一個 DbContext，
        //    所有 Repository／gate／UoW 共用同一個，交易才涵蓋得到（設計文件 06 第 3 節）──
        services.AddDbContext<TicketingDbContext>(o =>
            o.UseSqlServer(configuration.GetConnectionString("Ticketing"),
                sql => sql.CommandTimeout(10).EnableRetryOnFailure(2, TimeSpan.FromSeconds(2), null)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IBookingWriteGate, SqlBookingWriteGate>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPerformanceRepository, PerformanceRepository>();
        services.AddScoped<ISeatRepository, SeatRepository>();
        services.AddScoped<ISeatHoldRepository, SeatHoldRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        // ── DAO：表層存取，沒有聚合也沒有行為（ADR-2）──
        services.AddScoped<IIdempotencyDao, IdempotencyDao>();

        // ── 唯讀查詢：投影成 DTO，不經過 Domain（ADR-5）──
        services.AddScoped<ICatalogQueries, CatalogQueries>();
        services.AddScoped<IOrderQueries, OrderQueries>();

        // ── seed：CLI 與整合測試共用同一段程式 ──
        services.AddScoped<CatalogSeeder>();
        services.AddScoped<UserSeeder>();
        services.AddScoped<SeedRunner>();
        // 只 Bind，不加 ValidateOnStart：正式 API 啟動時本來就不該有 seed 密碼，
        // 在這裡驗證會讓整個 API 起不來。必填檢查在 SeedRunner 的 Preflight。
        services.AddOptions<SeedOptions>().Bind(configuration.GetSection("Seed"));

        // ── 安全：Singleton，無狀態或只有設定 ──
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IGoogleTokenVerifier, GoogleTokenVerifier>();

        // Jwt 與 Seed 相反：**一定要 ValidateOnStart**。少了簽章金鑰的 API
        // 不該啟動成功，再在第一次有人登入時才爆掉（設計文件 05 第 6 節）。
        services.AddOptions<JwtOptions>()
                .Bind(configuration.GetSection(JwtOptions.SectionName))
                .ValidateDataAnnotations()          // 必填與範圍
                .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();   // 金鑰長度與佔位字串

        services.AddOptions<GoogleAuthOptions>()
                .Bind(configuration.GetSection(GoogleAuthOptions.SectionName))
                .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GoogleAuthOptions>, GoogleAuthOptionsValidator>();

        // SeedRunner 是自己那條呼叫堆疊的頂端（CLI 進入點），需要時鐘來源。
        // AddApplication() 也會註冊 TimeProvider；用 TryAdd 讓兩個擴充方法各自完整、
        // 且呼叫順序不影響結果，不會重複註冊。
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
