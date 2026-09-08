using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ticketing.Infrastructure.Persistence;

/// <summary>
/// 啟動時印一行「實際生效」的 SQL 韌性參數。
///
/// 為什麼值得一個型別：在雲端排查冷啟動 500 的時候，
/// 「設定檔裡寫了 8 次重試」與「跑起來真的是 8 次重試」是兩件事——
/// appsettings.{Environment}.json 沒被打包進去、ASPNETCORE_ENVIRONMENT 設錯、
/// 綁定失敗但沒有拋例外，這三種都會安靜地退回預設值。
/// 有這一行，就能一眼分辨「設定沒讀到」與「讀到了但不夠」，不必回頭猜。
///
/// 為什麼是 hosted service 而不是在 Program.cs 印：
/// 註冊當下還沒有 logger，而 Api 只應該在 <c>AddInfrastructure</c> 一個地方
/// 認識 Infrastructure（設計文件 03 第 4.3 節、ADR-4）——
/// 讓 Api 為了印一行 log 而 using 到 Persistence 命名空間，就破壞了那條界線。
/// </summary>
/// <remarks>
/// 刻意**不注入** <c>IHostEnvironment</c>：整合測試是用裸的 <c>ServiceCollection</c>
/// 組起來的，沒有 Host，注入它會讓 76 個測試在 DI 驗證階段就掛掉（真的發生過）。
/// 而且也不需要——三個數字本身就是答案：production 印出 10／2／2，
/// 就代表 appsettings.Production.json 沒被讀到。
/// </remarks>
internal sealed class SqlResilienceStartupLog(
    SqlResilienceOptions options,
    ILogger<SqlResilienceStartupLog> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "SQL resilience in effect: CommandTimeout={CommandTimeoutSeconds}s "
            + "MaxRetryCount={MaxRetryCount} MaxRetryDelay={MaxRetryDelaySeconds}s",
            options.CommandTimeoutSeconds, options.MaxRetryCount, options.MaxRetryDelaySeconds);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
