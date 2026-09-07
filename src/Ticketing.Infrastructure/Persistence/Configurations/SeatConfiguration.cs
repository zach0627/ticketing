using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Domain.Booking;
using Ticketing.Domain.Catalog;

namespace Ticketing.Infrastructure.Persistence.Configurations;

public sealed class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.ToTable("Seats", t =>
        {
            t.HasCheckConstraint("CK_Seats_Number", "[RowNumber] > 0 AND [SeatNumber] > 0");

            // 約束 4：座位欄位永遠自洽。三個條件更新都同時改 Status 與兩個 nullable 欄位，
            // 少改一個就會被這條擋下來（設計文件 04 第 4 節）。
            t.HasCheckConstraint("CK_Seats_State", """
                ([Status] = 'Available' AND [HoldId] IS NULL     AND [HeldUntilUtc] IS NULL)
             OR ([Status] = 'Held'      AND [HoldId] IS NOT NULL AND [HeldUntilUtc] IS NOT NULL)
             OR ([Status] = 'Sold'      AND [HoldId] IS NOT NULL AND [HeldUntilUtc] IS NULL)
            """);
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Status).HasMaxLength(12).IsUnicode(false).HasConversion<string>();
        builder.Property(s => s.HeldUntilUtc).HasColumnType("datetimeoffset(7)");

        // 約束 5：不會有兩個「A 區 1 排 1 號」
        builder.HasIndex(s => new { s.PerformanceId, s.SectionId, s.RowNumber, s.SeatNumber })
               .IsUnique().HasDatabaseName("UQ_Seats_Address");

        // 整批售出／釋放走 HoldId，這個索引支援 MarkSoldAsync 與 ReleaseAsync
        builder.HasIndex(s => s.HoldId).HasDatabaseName("IX_Seats_HoldId");

        builder.HasOne<Performance>()
               .WithMany()
               .HasForeignKey(s => s.PerformanceId)
               .HasConstraintName("FK_Seats_Performance")
               .OnDelete(DeleteBehavior.NoAction);

        // 複合 FK：座位的 Section 必須屬於同一場
        builder.HasOne<Section>()
               .WithMany()
               .HasForeignKey(s => new { s.SectionId, s.PerformanceId })
               .HasPrincipalKey(sec => new { sec.Id, sec.PerformanceId })
               .HasConstraintName("FK_Seats_SectionPerformance")
               .OnDelete(DeleteBehavior.NoAction);

        // 複合 FK：座位目前的 HoldId 必須屬於同一場
        builder.HasOne<SeatHold>()
               .WithMany()
               .HasForeignKey(s => new { s.HoldId, s.PerformanceId })
               .HasPrincipalKey(h => new { h.Id, h.PerformanceId })
               .HasConstraintName("FK_Seats_HoldPerformance")
               .OnDelete(DeleteBehavior.NoAction);
    }
}
