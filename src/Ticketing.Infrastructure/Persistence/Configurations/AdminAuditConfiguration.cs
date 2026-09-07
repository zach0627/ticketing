using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Infrastructure.Persistence.Entities;

namespace Ticketing.Infrastructure.Persistence.Configurations;

public sealed class AdminAuditConfiguration : IEntityTypeConfiguration<AdminAudit>
{
    public void Configure(EntityTypeBuilder<AdminAudit> builder)
    {
        builder.ToTable("AdminAudits", t =>
            // 去重三欄位必須同時存在或同時為 NULL，否則重置重試會失去判斷依據
            t.HasCheckConstraint("CK_AdminAudits_Operation", """
                ([OperationId] IS NULL     AND [RequestHash] IS NULL     AND [ResultJson] IS NULL)
             OR ([OperationId] IS NOT NULL AND [RequestHash] IS NOT NULL AND [ResultJson] IS NOT NULL)
            """));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityColumn();        // 只有這張表用 identity
        builder.Property(a => a.Action).HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(a => a.RequestHash).HasColumnType("binary(32)");
        builder.Property(a => a.CreatedAtUtc).HasColumnType("datetimeoffset(7)");

        builder.HasIndex(a => a.OperationId).IsUnique()
               .HasFilter("[OperationId] IS NOT NULL")
               .HasDatabaseName("UX_AdminAudits_OperationId");
    }
}
