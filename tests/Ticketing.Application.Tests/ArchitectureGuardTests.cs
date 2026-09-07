using System.Reflection;
using Ticketing.Application;

namespace Ticketing.Application.Tests;

/// <summary>
/// Application 不能認識 EF：資料存取一律經由 Domain／Application 定義的介面，
/// 實作在 Infrastructure（設計文件 03 第 2 節、12 第 3 節）。
/// </summary>
public class ArchitectureGuardTests
{
    [Fact]
    public void Application_does_not_reference_entity_framework()
    {
        var application = typeof(ApplicationAssemblyMarker).Assembly;

        var offenders = application.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Microsoft.Data.SqlClient", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name)
            .ToArray();

        Assert.True(offenders.Length == 0,
            $"Application 不得參照 EF 或資料庫驅動，但參照了：{string.Join(", ", offenders)}");
    }
}
