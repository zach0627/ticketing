using Ticketing.Domain.Catalog;

namespace Ticketing.Domain.Booking;

/// <summary>
/// 純函式：在同一票區的可用座位裡找第一段同排連續 N 席。
/// 不跨排、不拆單、不挑「最好的」——找到第一段就回（設計文件 04 第 2.3 節）。
/// </summary>
public static class ContiguousSeatFinder
{
    /// <param name="available">同一票區目前可用的座位，順序不拘。</param>
    /// <param name="quantity">要幾席。</param>
    /// <returns>連續的座位，找不到回 <c>null</c>。</returns>
    public static IReadOnlyList<Seat>? Find(IEnumerable<Seat> available, int quantity)
    {
        ArgumentNullException.ThrowIfNull(available);

        if (quantity <= 0) return null;

        foreach (var row in available.GroupBy(s => s.RowNumber).OrderBy(g => g.Key))
        {
            var run = new List<Seat>();

            foreach (var seat in row.OrderBy(s => s.SeatNumber))
            {
                if (run.Count > 0 && seat.SeatNumber != run[^1].SeatNumber + 1)
                    run.Clear();                            // 號碼不連續，重新累積

                run.Add(seat);

                if (run.Count == quantity) return run;
            }
        }

        return null;
    }
}
