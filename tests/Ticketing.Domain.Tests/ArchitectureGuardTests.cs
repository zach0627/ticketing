using Ticketing.Domain.Common;

namespace Ticketing.Domain.Tests;

/// <summary>
/// 依賴方向由編譯器保證，這裡再用測試把「Domain 零套件」釘住：
/// 有人日後在 Domain 加了 EF、Newtonsoft 之類的套件，這個測試會紅燈。
/// 對應設計文件 03 第 2 節與 12 第 3 節的分層規則。
/// </summary>
public class ArchitectureGuardTests
{
    private static readonly string[] AllowedAssemblyPrefixes =
    [
        "System.", "System", "netstandard", "mscorlib", "Ticketing."
    ];

    [Fact]
    public void Domain_references_nothing_outside_the_base_class_library()
    {
        var domain = typeof(BookingRuleException).Assembly;

        var offenders = domain.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => !AllowedAssemblyPrefixes.Any(p =>
                name.Equals(p, StringComparison.Ordinal) || name.StartsWith(p, StringComparison.Ordinal)))
            .OrderBy(name => name)
            .ToArray();

        Assert.True(offenders.Length == 0,
            $"Domain 必須零套件，但參照了：{string.Join(", ", offenders)}");
    }
}
