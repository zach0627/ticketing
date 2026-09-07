using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Domain.Booking;
using Ticketing.Domain.Catalog;
using Ticketing.Domain.Orders;
using Ticketing.Domain.Users;

namespace Ticketing.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
            t.HasCheckConstraint("CK_Orders_Amount", "[TotalAmount] > 0 AND [Currency] = 'TWD'"));

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.TotalAmount).HasColumnType("decimal(10,2)");
        builder.Property(o => o.Currency).HasMaxLength(3).IsUnicode(false).IsFixedLength().HasDefaultValue("TWD");
        builder.Property(o => o.EventTitleSnapshot).HasMaxLength(100).IsRequired();
        builder.Property(o => o.StartsAtSnapshot).HasColumnType("datetimeoffset(7)");
        builder.Property(o => o.CreatedAtUtc).HasColumnType("datetimeoffset(7)");

        // 約束 2：一個保留最多變成一張訂單。重複付款寫不進第二筆。
        builder.HasIndex(o => o.HoldId).IsUnique().HasDatabaseName("UQ_Orders_Hold");

        builder.HasIndex(o => new { o.BuyerId, o.CreatedAtUtc, o.Id })
               .HasDatabaseName("IX_Orders_Buyer")
               .IsDescending(false, true, true);
        builder.HasIndex(o => new { o.BuyerId, o.PerformanceId })
               .HasDatabaseName("IX_Orders_BuyerPerformance");   // 限購查詢用

        builder.HasOne<AppUser>().WithMany().HasForeignKey(o => o.BuyerId)
               .HasConstraintName("FK_Orders_Buyer").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Performance>().WithMany().HasForeignKey(o => o.PerformanceId)
               .HasConstraintName("FK_Orders_Performance").OnDelete(DeleteBehavior.NoAction);

        // 複合 FK：訂單必須沿用原保留的買家與場次，不能張冠李戴
        builder.HasOne<SeatHold>().WithMany()
               .HasForeignKey(o => new { o.HoldId, o.BuyerId, o.PerformanceId })
               .HasPrincipalKey(h => new { h.Id, h.BuyerId, h.PerformanceId })
               .HasConstraintName("FK_Orders_HoldBuyerPerformance")
               .OnDelete(DeleteBehavior.NoAction);

        builder.Ignore(o => o.Items);   // 同 SeatHold：Items 是 _items 的唯讀投影

        builder.OwnsMany<OrderItem>("_items", items =>
        {
            items.ToTable("OrderItems", t => t.HasCheckConstraint("CK_OrderItems_Values",
                "[RowNumber] > 0 AND [SeatNumber] > 0 AND [UnitPrice] > 0"));

            items.WithOwner().HasForeignKey(i => i.OrderId);
            items.HasKey(i => new { i.OrderId, i.SeatId });
            items.Property(i => i.SectionCode).HasMaxLength(1).IsUnicode(false).IsRequired();
            items.Property(i => i.UnitPrice).HasColumnType("decimal(10,2)");
            items.Property(i => i.TicketCode).HasMaxLength(40).IsUnicode(false).IsRequired();

            // 約束 1：一席永遠只能賣一次。就算程式有 bug，第二張同席的票寫不進去。
            items.HasIndex(i => i.SeatId).IsUnique().HasDatabaseName("UQ_OrderItems_Seat");
            items.HasIndex(i => i.TicketCode).IsUnique().HasDatabaseName("UQ_OrderItems_Ticket");

            items.HasOne<Seat>().WithMany().HasForeignKey(i => i.SeatId)
                 .HasConstraintName("FK_OrderItems_Seat").OnDelete(DeleteBehavior.NoAction);
        });

        builder.Navigation("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
