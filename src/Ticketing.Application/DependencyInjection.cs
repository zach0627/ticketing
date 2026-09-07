using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ticketing.Application;

/// <summary>
/// Application 的註冊集中在這裡（設計文件 03 第 4.3 節、ADR-4）。
///
/// **對應器不在這裡**：Mapperly 產生的是編譯期靜態方法，不需要註冊，
/// 也沒有授權金鑰要傳（ADR-10）。
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 業務時間的來源。測試換成 FakeTimeProvider 撥動售票與保留的時間。
        // AddInfrastructure() 也會 TryAdd 一份，讓兩個模組各自完整、順序無關。
        services.TryAddSingleton(TimeProvider.System);

        // Service（IAuthService、IBookingService、IAdminService）自階段 5 起陸續加入。

        return services;
    }
}
