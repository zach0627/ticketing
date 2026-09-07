// 階段 4：第一條垂直切片（瀏覽器 → Controller → Queries → SQL）。
// 組裝順序見設計文件 03 第 6 節；JWT、限流、seed 以外的 middleware 於後續階段接上。

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Ticketing.Api.Commands;
using Ticketing.Api.ExceptionHandling;
using Ticketing.Application;
using Ticketing.Domain.Common;
using Ticketing.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Log：Serilog 取代預設 provider；業務程式仍然只用 ILogger<T> ──
builder.Services.AddSerilog((_, cfg) => cfg
    .ReadFrom.Configuration(builder.Configuration)      // 等級與輸出格式在 appsettings
    .Enrich.FromLogContext());

// ── 容器嚴格模式：漏註冊、Singleton 抓 Scoped，啟動就失敗（03 第 4.3 節）──
builder.Host.UseDefaultServiceProvider(o =>
{
    o.ValidateOnBuild = true;
    o.ValidateScopes = true;
});

// ── 兩層各自註冊自己，Api 只認識這兩行 ──
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Api 自己的東西 ──
builder.Services.AddSingleton<ApiProblemWriter>();          // 無 scoped 相依
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        // enum 一律輸出字串（"OnSale"），不暴露數字——中間插入新 enum 值時前端不會錯亂
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });

// 模型驗證失敗（型別錯、超出 Range、缺欄位）也要走同一個錯誤格式。
// AddProblemDetails() 不會自動幫我們補上 code，必須自己接。
builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = context =>
    {
        var writer = context.HttpContext.RequestServices.GetRequiredService<ApiProblemWriter>();
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(e => e.Key, object? (e) => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());

        var problem = writer.Create(context.HttpContext, ErrorCode.ValidationFailed,
            "請求內容未通過驗證", new Dictionary<string, object?> { ["errors"] = errors });

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    };
});

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();     // 刻意不查資料庫：避免阻止 Azure SQL 閒置自動暫停（09）

builder.Services.AddCors(o => o.AddPolicy("Frontend", p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Retry-After", "Location")));

var app = builder.Build();

// ── CLI 指令：dotnet run -- seed / promote-admin <email>
//    不在 API 啟動時自動 migrate 或 seed（設計文件 15 第 1 節）──
if (args is ["seed"] or ["promote-admin", _])
{
    // SeedRunner 是 Scoped：ValidateScopes 開著時不能直接從 root provider 解析
    using var seedScope = app.Services.CreateScope();
    return await SeedCommand.RunAsync(seedScope.ServiceProvider, args);
}

// ── middleware 順序（設計文件 03 第 6 節）──
app.UseExceptionHandler();              // ① 最外層：任何例外 → ProblemDetails
app.UseSerilogRequestLogging();         // ② 一行一請求：方法、路徑、狀態碼、耗時

// 空 body 的狀態錯誤（404 路由不符、405 方法不支援）也補上一致的格式
app.UseStatusCodePages(async context =>
{
    var writer = context.HttpContext.RequestServices.GetRequiredService<ApiProblemWriter>();
    var code = context.HttpContext.Response.StatusCode switch
    {
        StatusCodes.Status404NotFound => ErrorCode.NotFound,
        StatusCodes.Status405MethodNotAllowed => ErrorCode.MethodNotAllowed,
        StatusCodes.Status413PayloadTooLarge => ErrorCode.PayloadTooLarge,
        StatusCodes.Status415UnsupportedMediaType => ErrorCode.UnsupportedMediaType,
        _ => ErrorCode.InternalError
    };
    await writer.WriteAsync(context.HttpContext, code);
});

if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();   // 本機走 HTTP（08）

app.UseRouting();
app.UseCors("Frontend");                // ⑤ 在 Authentication 之前

app.MapControllers();
app.MapHealthChecks("/health");
app.MapOpenApi();

await app.RunAsync();
return 0;

// WebApplicationFactory<Program> 需要這個型別可被測試專案參照（設計文件 10 第 1.2 節）。
public partial class Program;
