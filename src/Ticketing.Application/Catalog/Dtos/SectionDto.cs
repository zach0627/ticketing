namespace Ticketing.Application.Catalog.Dtos;

/// <summary>票區。純欄位對應，由 Mapperly 的投影方法產生（設計文件 13 第 2.1 節）。</summary>
public sealed record SectionDto(
    int Id,
    string Code,
    string Name,
    decimal Price,
    int RowCount,
    int SeatsPerRow);
