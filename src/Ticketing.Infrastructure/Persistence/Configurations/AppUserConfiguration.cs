using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Domain.Users;

namespace Ticketing.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUsers", t =>
        {
            t.HasCheckConstraint("CK_AppUsers_Role", "[Role] IN ('Customer','Admin')");
            // 至少要有一種身份。Domain 的 CreateWithPassword 先建 user 再設雜湊，
            // 中間那一瞬間不合法，所以這條只能由資料庫把關（設計文件 04 第 3 節）。
            t.HasCheckConstraint("CK_AppUsers_Identity", "[PasswordHash] IS NOT NULL OR [GoogleSubject] IS NOT NULL");
        });

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Email).HasMaxLength(254).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(80).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(200);
        builder.Property(u => u.GoogleSubject).HasMaxLength(64).IsUnicode(false);
        builder.Property(u => u.Role).HasMaxLength(10).IsUnicode(false).HasConversion<string>();
        builder.Property(u => u.CreatedAtUtc).HasColumnType("datetimeoffset(7)");

        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UQ_AppUsers_Email");
        builder.HasIndex(u => u.GoogleSubject).IsUnique()
               .HasFilter("[GoogleSubject] IS NOT NULL")
               .HasDatabaseName("UX_AppUsers_GoogleSubject");
    }
}
