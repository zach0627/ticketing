using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ticketing.Infrastructure.Persistence.Seeding;

/// <summary>`catalog.seed.json` 的形狀。`queueEnabled` 是第二階段候位室的旗標，第一版忽略。</summary>
public sealed class CatalogSeedFile
{
    public int SchemaVersion { get; set; }
    public int HoldMinutes { get; set; }
    public List<CatalogSeedEvent> Events { get; set; } = [];

    public static CatalogSeedFile Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<CatalogSeedFile>(stream, Options)
               ?? throw new InvalidOperationException($"無法解析 seed 檔：{path}");
    }

    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}

public sealed class CatalogSeedEvent
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Performer { get; set; } = "";
    public string Genre { get; set; } = "";
    public string City { get; set; } = "";
    public string Venue { get; set; } = "";
    public string Description { get; set; } = "";
    public int PerformanceId { get; set; }
    public int StartsAfterDays { get; set; }
    public string StartsAtTaipei { get; set; } = "";
    public int DurationMinutes { get; set; }
    public int SalesOpenOffsetDays { get; set; }
    public int SalesCloseMinutesBeforeStart { get; set; }
    public string PublicImage { get; set; } = "";
    public List<CatalogSeedSection> Sections { get; set; } = [];
}

public sealed class CatalogSeedSection
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Rows { get; set; }
    public int SeatsPerRow { get; set; }
}

public sealed class PresetUserSeedFile
{
    public List<PresetUserSeed> Users { get; set; } = [];

    public static PresetUserSeedFile Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<PresetUserSeedFile>(stream, CatalogSeedFile.Options)
               ?? throw new InvalidOperationException($"無法解析 seed 檔：{path}");
    }
}

public sealed class PresetUserSeed
{
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
}
