// 階段 3：接上 AddInfrastructure 與 seed CLI。
// 其餘組裝（Serilog、AddApplication、JWT、CORS、限流、middleware 順序）
// 見設計文件 03 第 6 節，於後續階段逐步接上。

using Ticketing.Api.Commands;
using Ticketing.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 容器嚴格模式：漏註冊、Singleton 抓 Scoped，啟動就失敗（03 第 4.3 節）
builder.Host.UseDefaultServiceProvider(o =>
{
    o.ValidateOnBuild = true;
    o.ValidateScopes = true;
});

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();   // 刻意不查資料庫（09：避免阻止 Azure SQL 閒置自動暫停）

var app = builder.Build();

// ── CLI 指令：dotnet run -- seed / promote-admin <email>
//    不在 API 啟動時自動 migrate 或 seed（設計文件 15 第 1 節）──
if (args is ["seed"] or ["promote-admin", _])
{
    // SeedRunner 是 Scoped：ValidateScopes 開著時不能直接從 root provider 解析
    using var seedScope = app.Services.CreateScope();
    return await SeedCommand.RunAsync(seedScope.ServiceProvider, args);
}

app.MapOpenApi();
app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();
return 0;

// WebApplicationFactory<Program> 需要這個型別可被測試專案參照（設計文件 10 第 1.2 節）。
public partial class Program;
