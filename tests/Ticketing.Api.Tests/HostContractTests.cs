namespace Ticketing.Api.Tests;

/// <summary>
/// 整合測試要用 <c>WebApplicationFactory&lt;Program&gt;</c> 啟動真的 API，
/// 前提是 Api 的 Program 型別看得到（設計文件 10 第 1.2 節）。
/// 真正的 HTTP 與併發測試在階段 3 之後加入，需要 Testcontainers。
/// </summary>
public class HostContractTests
{
    [Fact]
    public void Program_type_is_visible_to_the_test_project()
    {
        Assert.NotNull(typeof(Program));
    }
}
