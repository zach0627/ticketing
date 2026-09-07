using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Domain.Catalog;

namespace Ticketing.Infrastructure.Persistence.Configurations;

public sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("Sections", t =>
        {
            t.HasCheckConstraint("CK_Sections_Code", "[Code] IN ('A','B','C')");
            t.HasCheckConstraint("CK_Sections_Values",
                "[Price] > 0 AND [RowCount] > 0 AND [SeatsPerRow] > 0");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Code).HasMaxLength(1).IsUnicode(false).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(40).IsRequired();
        builder.Property(s => s.Price).HasColumnType("decimal(10,2)");
        builder.Ignore(s => s.Capacity);                            // 算出來的，不存欄位

        builder.HasIndex(s => new { s.PerformanceId, s.Code })
               .IsUnique().HasDatabaseName("UQ_Sections_PerformanceCode");

        // 給 Seats 的複合 FK 用：保證座位的 Section 與 Performance 是同一場
        builder.HasAlternateKey(s => new { s.Id, s.PerformanceId })
               .HasName("UQ_Sections_IdPerformance");

        builder.HasOne<Performance>()
               .WithMany()
               .HasForeignKey(s => s.PerformanceId)
               .HasConstraintName("FK_Sections_Performance")
               .OnDelete(DeleteBehavior.NoAction);
    }
}
