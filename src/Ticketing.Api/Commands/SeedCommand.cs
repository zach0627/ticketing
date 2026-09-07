using Ticketing.Infrastructure.Persistence.Seeding;

namespace Ticketing.Api.Commands;

/// <summary>
/// <c>dotnet run -- seed</c> / <c>promote-admin &lt;email&gt;</c> 的入口。
/// Api 只負責解析參數與印結果，seed 邏輯全部在 Infrastructure（設計文件 15 第 3 節）。
/// 呼叫端已經開好 scope——<see cref="SeedRunner"/> 是 Scoped，
/// `ValidateScopes` 開著時不能從 root provider 解析。
/// </summary>
public static class SeedCommand
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, CancellationToken ct = default)
    {
        var runner = services.GetRequiredService<SeedRunner>();

        try
        {
            switch (args)
            {
                case ["seed"]:
                    var result = await runner.RunAsync(ct);
                    Console.WriteLine(result);
                    return 0;

                case ["promote-admin", var email]:
                    var promoted = await runner.PromoteAdminAsync(email, ct);
                    Console.WriteLine(promoted ? $"Promoted: {email}" : $"AlreadyAdmin: {email}");
                    return 0;

                default:
                    Console.Error.WriteLine("用法：dotnet run -- seed | promote-admin <email>");
                    return 2;
            }
        }
        catch (Exception ex)
        {
            // 只印訊息，不印堆疊或任何設定值
            Console.Error.WriteLine($"失敗：{ex.Message}");
            return 1;
        }
    }
}
