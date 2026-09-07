using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Domain.Booking;
using Ticketing.Domain.Catalog;
using Ticketing.Domain.Users;

namespace Ticketing.Infrastructure.Persistence.Configurations;

public sealed class SeatHoldConfiguration : IEntityTypeConfiguration<SeatHold>
{
    public void Configure(EntityTypeBuilder<SeatHold> builder)
    {
        builder.ToTable("SeatHolds", t =>
        {
            t.HasCheckConstraint("CK_SeatHolds_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
            t.HasCheckConstraint("CK_SeatHolds_Status",
                "[Status] IN ('Active','Completed','Cancelled','Expired')");
            t.HasCheckConstraint("CK_SeatHolds_Amount", "[TotalAmount] > 0");
        });

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.Status).HasMaxLength(12).IsUnicode(false).HasConversion<string>();
        builder.Property(h => h.CreatedAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(h => h.ExpiresAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(h => h.TotalAmount).HasColumnType("decimal(10,2)");

        // 約束 3：每人每場最多一筆資料庫狀態 Active。
        // 注意「時間到」不會自動退出這個索引——新保留必須先把舊的持久化成 Expired
        // （設計文件 04 第 4 節、06 第 4 節第 4 步）。
        builder.HasIndex(h => new { h.BuyerId, h.PerformanceId })
               .IsUnique()
               .HasFilter("[Status] = 'Active'")
               .HasDatabaseName("UX_SeatHolds_ActiveBuyerPerformance");

        // 給 Seats 與 Orders 的複合 FK 用
        builder.HasAlternateKey(h => new { h.Id, h.PerformanceId }).HasName("UQ_SeatHolds_IdPerformance");
        builder.HasAlternateKey(h => new { h.Id, h.BuyerId, h.PerformanceId })
               .HasName("UQ_SeatHolds_IdBuyerPerformance");

        builder.HasOne<AppUser>().WithMany().HasForeignKey(h => h.BuyerId)
               .HasConstraintName("FK_SeatHolds_Buyer").OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Performance>().WithMany().HasForeignKey(h => h.PerformanceId)
               .HasConstraintName("FK_SeatHolds_Performance").OnDelete(DeleteBehavior.NoAction);

        // Items 是 _items 的唯讀投影（AsReadOnly），不是另一條關聯——
        // 不忽略的話 EF 會把它當成第二個 navigation 而無法決定關聯。
        builder.Ignore(h => h.Items);

        // Items 是私有集合，用 backing field 讓 EF 填得進去（Domain 沒有公開的 setter）
        builder.OwnsMany<SeatHoldItem>("_items", items =>
        {
            items.ToTable("SeatHoldItems", t => t.HasCheckConstraint("CK_SeatHoldItems_Values",
                "[RowNumber] > 0 AND [SeatNumber] > 0 AND [UnitPrice] > 0"));

            items.WithOwner().HasForeignKey("HoldId");
            items.HasKey("HoldId", nameof(SeatHoldItem.SeatId));
            items.Property(i => i.SectionCode).HasMaxLength(1).IsUnicode(false).IsRequired();
            items.Property(i => i.UnitPrice).HasColumnType("decimal(10,2)");

            items.HasOne<Seat>().WithMany().HasForeignKey(i => i.SeatId)
                 .HasConstraintName("FK_SeatHoldItems_Seat").OnDelete(DeleteBehavior.NoAction);
        });

        builder.Navigation("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
