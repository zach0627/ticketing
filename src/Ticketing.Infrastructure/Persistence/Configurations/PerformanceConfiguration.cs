using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Domain.Catalog;

namespace Ticketing.Infrastructure.Persistence.Configurations;

public sealed class PerformanceConfiguration : IEntityTypeConfiguration<Performance>
{
    public void Configure(EntityTypeBuilder<Performance> builder)
    {
        builder.ToTable("Performances", t =>
        {
            t.HasCheckConstraint("CK_Performances_Times",
                "[SalesOpensAtUtc] < [SalesClosesAtUtc] AND [SalesClosesAtUtc] < [StartsAtUtc]");
            t.HasCheckConstraint("CK_Performances_Duration", "[DurationMinutes] > 0");
        });

        // gate 的 SQL 明確指定 PK_Performances 這個 clustered 索引，
        // 兩邊必須鎖同一個資源，名稱不能改（設計文件 06 第 3.1 節）
        builder.HasKey(p => p.Id).HasName("PK_Performances").IsClustered();
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Venue).HasMaxLength(100).IsRequired();
        builder.Property(p => p.City).HasMaxLength(30).IsRequired();
        builder.Property(p => p.StartsAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(p => p.SalesOpensAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(p => p.SalesClosesAtUtc).HasColumnType("datetimeoffset(7)");
        builder.Property(p => p.IsSalesPaused).HasDefaultValue(false);

        builder.HasOne(p => p.Event)
               .WithMany()
               .HasForeignKey(p => p.EventId)
               .HasConstraintName("FK_Performances_Event")
               .OnDelete(DeleteBehavior.NoAction);
    }
}
