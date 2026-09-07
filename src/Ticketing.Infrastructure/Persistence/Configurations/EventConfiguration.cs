using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Domain.Catalog;

namespace Ticketing.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events", t =>
            t.HasCheckConstraint("CK_Events_Category", "[Category] IN ('Concert','Sport')"));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();          // seed 指定 ID
        builder.Property(e => e.Code).HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(10).IsUnicode(false).HasConversion<string>();
        builder.Property(e => e.Title).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Performer).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Genre).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1200).IsRequired();
        builder.Property(e => e.ImagePath).HasMaxLength(200).IsRequired();
        builder.Property(e => e.IsPublished).HasDefaultValue(true);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UQ_Events_Code");
    }
}
