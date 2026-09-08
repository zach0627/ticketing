using Microsoft.EntityFrameworkCore;
using Ticketing.Application.Common;
using Ticketing.Application.Orders;
using Ticketing.Application.Orders.Dtos;
using Ticketing.Domain.Orders;

namespace Ticketing.Infrastructure.Persistence.Queries;

/// <summary>
/// 唯讀查詢：<c>AsNoTracking</c> ＋ 直接投影成 DTO，**不經過 Domain**（ADR-5）。
///
/// 列表刻意不查明細，只算張數——訂單列表要顯示的是「幾張、多少錢」，
/// 不需要把每一張票都撈回來（設計文件 13 第 2.1 節）。
/// </summary>
public sealed class OrderQueries(TicketingDbContext db) : IOrderQueries
{
    public async Task<PagedResult<OrderSummaryDto>> GetMyOrdersAsync(Guid buyerId, int page, int pageSize,
                                                                     CancellationToken ct)
    {
        var mine = db.Orders.AsNoTracking().Where(o => o.BuyerId == buyerId);

        var total = await mine.CountAsync(ct);

        var items = await mine
            // tie-breaker 一定要有：只用 CreatedAtUtc 排序時，同一毫秒的兩筆在不同頁可能重複或漏掉
            .OrderByDescending(o => o.CreatedAtUtc).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrderSummaryDto(
                o.Id,
                o.HoldId,
                o.EventTitleSnapshot,
                o.StartsAtSnapshot,
                EF.Property<IReadOnlyList<OrderItem>>(o, "_items").Count,
                o.TotalAmount,
                o.Currency,
                o.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<OrderSummaryDto>(items, total);
    }

    /// <summary>
    /// <c>buyerId</c> 在 WHERE 裡，不是查到之後才比對——
    /// 別人的訂單根本查不出來，自然就是 404 而不是 403。
    /// </summary>
    public Task<OrderDto?> GetMyOrderAsync(Guid buyerId, Guid orderId, CancellationToken ct)
        => db.Orders.AsNoTracking()
             .Where(o => o.Id == orderId && o.BuyerId == buyerId)
             .Select(o => new OrderDto(
                 o.Id,
                 o.HoldId,
                 o.EventTitleSnapshot,
                 o.StartsAtSnapshot,
                 o.Currency,
                 o.TotalAmount,
                 o.CreatedAtUtc,
                 EF.Property<IReadOnlyList<OrderItem>>(o, "_items")
                   .OrderBy(i => i.SeatId)
                   .Select(i => new OrderItemDto(i.SectionCode, i.RowNumber, i.SeatNumber,
                                                 i.UnitPrice, i.TicketCode))
                   .ToList()))
             .FirstOrDefaultAsync(ct);
}
