using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Domain.Users;
using Ticketing.Infrastructure.Persistence.Entities;

namespace Ticketing.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");

        // 約束 6：同 key 只能寫一筆——冪等機制的最終保證
        builder.HasKey(r => new { r.BuyerId, r.Key });

        builder.Property(r => r.Key).HasMaxLength(64).IsUnicode(false);
        builder.Property(r => r.RequestHash).HasColumnType("binary(32)").IsRequired();
        builder.Property(r => r.ResponseJson).IsRequired();
        builder.Property(r => r.CreatedAtUtc).HasColumnType("datetimeoffset(7)");

        builder.HasOne<AppUser>().WithMany().HasForeignKey(r => r.BuyerId)
               .HasConstraintName("FK_Idem_Buyer").OnDelete(DeleteBehavior.NoAction);
    }
}
