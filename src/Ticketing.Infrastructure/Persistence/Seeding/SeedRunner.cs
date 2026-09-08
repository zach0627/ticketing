using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticketing.Domain.Common;
using Ticketing.Domain.Users;

namespace Ticketing.Infrastructure.Persistence.Seeding;

/// <summary>
/// seed 的進入點。CLI 與整合測試走同一段程式（設計文件 15 第 3 節）。
///
/// 流程：① Preflight（缺必要設定就非零退出，不寫任何資料）→ ② 開交易
/// → ③ catalog → ④⑤ 帳號 → ⑥ SaveChanges／Commit。
/// </summary>
public sealed class SeedRunner(
    IUnitOfWork uow,
    IUserRepository users,
    CatalogSeeder catalogSeeder,
    UserSeeder userSeeder,
    IOptions<SeedOptions> options,
    TimeProvider clock,
    ILogger<SeedRunner> logger)
{
    private static readonly string SeedingDirectory =
        Path.Combine(AppContext.BaseDirectory, "Persistence", "Seeding");

    public async Task<SeedResult> RunAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var (catalogFile, presetFile) = Preflight(settings);

        var now = clock.GetUtcNow();

        return await uow.ExecuteInTransactionAsync(async token =>
        {
            var catalog = await catalogSeeder.SeedAsync(catalogFile, now, token);
            var users = await userSeeder.SeedAsync(settings, presetFile, now, token);

            await uow.SaveChangesAsync(token);

            var created = catalog.Events + catalog.Seats + users;
            var outcome = created > 0 ? SeedOutcome.Seeded : SeedOutcome.AlreadySeeded;

            var result = new SeedResult(outcome, catalog.Events, catalog.Performances,
                                        catalog.Sections, catalog.Seats, users);
            logger.LogInformation("Seed 完成：{Result}", result);
            return result;
        }, ct);
    }

    /// <summary>
    /// 缺必要設定就在寫任何資料**之前**失敗。錯誤訊息只說缺哪個設定鍵，不印出值。
    /// </summary>
    private static (CatalogSeedFile Catalog, PresetUserSeedFile Presets) Preflight(SeedOptions settings)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.Admin.Email)) missing.Add("Seed:Admin:Email");
        if (string.IsNullOrWhiteSpace(settings.Admin.Password)) missing.Add("Seed:Admin:Password");
        if (settings.PresetUsers.Enabled && string.IsNullOrWhiteSpace(settings.PresetUsers.Password))
            missing.Add("Seed:PresetUsers:Password");

        if (missing.Count > 0)
            throw new InvalidOperationException(
                "缺少 seed 必要設定：" + string.Join("、", missing)
                + "。請用 dotnet user-secrets 設定（本機）或應用程式設定（雲端），不要寫進 git。");

        var catalogPath = Path.Combine(SeedingDirectory, "catalog.seed.json");
        var presetPath = Path.Combine(SeedingDirectory, "users.seed.json");

        foreach (var path in new[] { catalogPath, presetPath })
            if (!File.Exists(path))
                throw new InvalidOperationException($"找不到 seed 檔：{path}");

        var catalog = CatalogSeedFile.Load(catalogPath);
        var presets = PresetUserSeedFile.Load(presetPath);

        if (catalog.Events.Count == 0)
            throw new InvalidOperationException("catalog.seed.json 沒有任何活動");

        return (catalog, presets);
    }

    /// <summary>把既有帳號升成管理者。原本就是 Admin 回 false（不重複寫稽核）。</summary>
    public async Task<bool> PromoteAdminAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();

        return await uow.ExecuteInTransactionAsync(async token =>
        {
            var user = await users.GetByEmailAsync(normalized, token)
                       ?? throw new InvalidOperationException($"找不到帳號：{normalized}");

            if (user.Role == UserRole.Admin) return false;

            user.PromoteToAdmin();
            await uow.SaveChangesAsync(token);
            // 記 Id 不記 Email：log 不得含 Email（設計文件 10 第 3 節），
            // 而操作者本來就知道自己輸入了哪個 Email——找不到的話上面已經回報過了。
            logger.LogInformation("AdminPromoted {UserId}", user.Id);
            return true;
        }, ct);
    }
}
