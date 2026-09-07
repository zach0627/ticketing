// 階段 1（骨架）：只組裝到「能啟動、能回應 /health」為止。
// 完整的組裝順序（Serilog、AddApplication／AddInfrastructure、JWT、CORS、限流、
// middleware 順序、seed CLI 入口）見設計文件 03 第 6 節，於後續階段逐步接上。

var builder = WebApplication.CreateBuilder(args);

// 容器嚴格模式：漏註冊、Singleton 抓 Scoped，啟動就失敗（03 第 4.3 節）
builder.Host.UseDefaultServiceProvider(o =>
{
    o.ValidateOnBuild = true;
    o.ValidateScopes = true;
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();   // 刻意不查資料庫（09：避免阻止 Azure SQL 閒置自動暫停）

var app = builder.Build();

app.MapOpenApi();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// WebApplicationFactory<Program> 需要這個型別可被測試專案參照（設計文件 10 第 1.2 節）。
public partial class Program;
