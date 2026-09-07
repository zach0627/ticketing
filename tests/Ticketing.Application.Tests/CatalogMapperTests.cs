using Ticketing.Application.Catalog;
using Ticketing.Domain.Catalog;

namespace Ticketing.Application.Tests;

/// <summary>
/// M01-Catalog：驗**對應出來的值**，不只是「有沒有欄位」。
///
/// 每個欄位刻意給**不同的值**——如果全部都是 1，把 RowNumber 對應到 SeatNumber
/// 這種錯誤會安靜地通過（設計文件 10 第 2 節）。
///
/// RMG012／RMG020 只保證「沒有欄位被漏掉」，不保證「對到正確的來源」。
/// 那是這組測試的工作。
/// </summary>
public class CatalogMapperTests
{
    [Fact]
    public void Section_maps_every_field_to_the_right_source()
    {
        var section = new Section(
            id: 42,
            performanceId: 7,
            code: "B",
            name: "中區",
            price: 1234.50m,
            rowCount: 5,
            seatsPerRow: 12);                 // ← rowCount 與 seatsPerRow 刻意不同，避免對調也通過

        var dto = CatalogMapper.ToDto(section);

        Assert.Equal(42, dto.Id);
        Assert.Equal("B", dto.Code);
        Assert.Equal("中區", dto.Name);
        Assert.Equal(1234.50m, dto.Price);
        Assert.Equal(5, dto.RowCount);
        Assert.Equal(12, dto.SeatsPerRow);
    }

    [Fact]
    public void Section_dto_does_not_expose_the_performance_id_or_computed_capacity()
    {
        // DTO 刻意少這兩個欄位（用 [MapperIgnoreSource] 明確忽略）：
        // PerformanceId 呼叫端已經知道；Capacity 是算出來的。
        var properties = typeof(Application.Catalog.Dtos.SectionDto)
            .GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain(nameof(Section.PerformanceId), properties);
        Assert.DoesNotContain(nameof(Section.Capacity), properties);
        Assert.Equal(6, properties.Length);
    }

    [Fact]
    public void Decimal_prices_keep_their_scale()
    {
        // 金額用 decimal，不是 double——0.1 + 0.2 != 0.3 那種問題不能出現在錢上面
        var dto = CatalogMapper.ToDto(new Section(1, 1, "A", "前區", 2100.05m, 1, 1));

        Assert.Equal(2100.05m, dto.Price);
        Assert.IsType<decimal>(dto.Price);
    }
}
