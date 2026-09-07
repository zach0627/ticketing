using System.ComponentModel.DataAnnotations;

namespace Ticketing.Infrastructure.Persistence.Seeding;

/// <summary>
/// 綁 <c>Seed:*</c> 設定。**刻意不加 ValidateOnStart**：
/// 正式 API 啟動時本來就不該有 seed 密碼，在容器啟動時驗證會讓整個 API 起不來。
/// 必填檢查在 <see cref="SeedRunner"/> 的 Preflight，只在 seed 路徑執行（設計文件 15 第 3 節）。
/// </summary>
public sealed class SeedOptions
{
    public AdminSeedOptions Admin { get; set; } = new();
    public PresetUsersSeedOptions PresetUsers { get; set; } = new();
}

public sealed class AdminSeedOptions
{
    [EmailAddress]
    public string? Email { get; set; }
    public string? Password { get; set; }
}

public sealed class PresetUsersSeedOptions
{
    public bool Enabled { get; set; }
    public string? Password { get; set; }
}
