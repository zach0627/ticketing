// 組裝點。順序見設計文件 03 第 6 節。
// 階段 4 立起 HTTP 骨幹，階段 5 補上認證、授權與限流。

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using Serilog;
using Ticketing.Api.Auth;
using Ticketing.Api.Commands;
using Ticketing.Api.ExceptionHandling;
using Ticketing.Api.RateLimiting;
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

// 密碼雜湊與 Google 驗簽都不便宜，不能讓人送 10 MB 的 body 逼我們去跑。
// 欄位長度限制（05 第 4.1 節）與這個上限是一起生效的兩道門。
builder.Services.Configure<KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = 16 * 1024);

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

// ── 認證與授權（設計文件 05）──
builder.Services.AddHttpContextAccessor();                 // CurrentUser 的建構相依
builder.Services.AddScoped<CurrentUser>();                 // 一個請求一份

// JwtBearerOptions 交給 ConfigureJwtBearerOptions 設定，才拿得到已驗證的 JwtOptions
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorization();                       // [Authorize(Roles = "Admin")] 就夠用

builder.Services.AddTicketingRateLimiter(builder.Configuration);

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

// ⚠️ 請求記錄要在例外處理**外面**。
// 反過來的話，業務例外會先穿過 Serilog 才被 UseExceptionHandler 轉成 409／401，
// 於是 log 記成「responded 500 [ERR]」——但使用者實際收到的是 409。
// 結果是滿畫面假的 500，真正的伺服器錯誤反而被淹沒（設計文件 10 第 3 節）。
// 放在外面，Serilog 看到的是**已經轉換完成**的狀態碼；未處理例外仍會是 500 → Error，
// 而且堆疊由 ApiExceptionHandler 自己記一次，資訊沒有變少。
app.UseSerilogRequestLogging();         // ① 一行一請求：方法、路徑、最終狀態碼、耗時
app.UseExceptionHandler();              // ② 任何例外 → ProblemDetails

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

// HTTP → HTTPS 轉址「由誰負責」是部署決定，不是程式碼決定：
//   本機：不需要（08）
//   App Service：平台的「HTTPS Only」已經在前端做完了，應用程式再做一次會無窮迴圈——
//                平台以 http 把請求轉進來，我們導去 https，平台再轉進來……（16 第 8 節）
// 所以做成設定，預設開啟；appsettings.Production.json 關掉它。
if (builder.Configuration.GetValue("Hosting:HttpsRedirection", !app.Environment.IsDevelopment()))
    app.UseHttpsRedirection();

// ⚠️ 刻意**不啟用** ForwardedHeaders。
// 在 App Service 上沒辦法列舉可信代理的位址，而清空 KnownProxies 等於相信任何人送來的
// X-Forwarded-For——那會讓每個請求都能自稱來自不同 IP，限流形同虛設（設計文件 05 第 7 節）。
// 代價講明白：雲端上 auth 端點的限流會退化成**全站共用一個額度**，
// 而不是每個真實 IP 一份。這是保守但誠實的選擇（16 第 8 節）。

app.UseRouting();
app.UseCors("Frontend");                // ⑤ 在 Authentication 之前
app.UseAuthentication();                // ⑥ 你是誰
app.UseAuthorization();                 // ⑦ 你能做什麼
app.UseRateLimiter();                   // ⑧ 知道你是誰之後才能按人限流（booking／admin 用 sub 分流）

app.MapControllers();
app.MapHealthChecks("/health");
app.MapOpenApi();

await app.RunAsync();
return 0;

// WebApplicationFactory<Program> 需要這個型別可被測試專案參照（設計文件 10 第 1.2 節）。
public partial class Program;
