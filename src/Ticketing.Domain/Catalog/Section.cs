using Ticketing.Domain.Common;

namespace Ticketing.Domain.Catalog;

/// <summary>場次的一個價區（A／B／C）。單價是保留與訂單快照的來源。</summary>
public sealed class Section
{
    public int Id { get; private set; }
    public int PerformanceId { get; private set; }
    public string Code { get; private set; } = "";          // A / B / C
    public string Name { get; private set; } = "";
    public decimal Price { get; private set; }              // 金額一律 decimal，永遠不用 double
    public int RowCount { get; private set; }
    public int SeatsPerRow { get; private set; }

    private Section() { }                                   // EF Core 用

    public Section(int id, int performanceId, string code, string name,
                   decimal price, int rowCount, int seatsPerRow)
    {
        if (id <= 0 || performanceId <= 0 || string.IsNullOrWhiteSpace(code))
            throw new BookingRuleException(ErrorCode.ValidationFailed, "票區的 Id、場次與代碼不可為空");
        if (price <= 0 || rowCount <= 0 || seatsPerRow <= 0)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "票區的單價、排數與每排席數必須大於 0");

        Id = id;
        PerformanceId = performanceId;
        Code = code;
        Name = name;
        Price = price;
        RowCount = rowCount;
        SeatsPerRow = seatsPerRow;
    }

    public int Capacity => RowCount * SeatsPerRow;
}
