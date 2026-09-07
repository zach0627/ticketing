using Ticketing.Application.Abstractions;
using Ticketing.Domain.Users;

namespace Ticketing.Infrastructure.Persistence.Seeding;

/// <summary>
/// 管理者與預建帳號。**只補缺少的，不重設既有帳號的密碼或角色**（設計文件 15 第 3 節）。
/// </summary>
public sealed class UserSeeder(TicketingDbContext db, IUserRepository users, IPasswordHasher hasher)
{
    public async Task<int> SeedAsync(SeedOptions options, PresetUserSeedFile presets,
                                     DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(presets);

        var created = 0;
        var adminEmail = Normalize(options.Admin.Email!);

        var admin = await users.GetByEmailAsync(adminEmail, ct);
        if (admin is null)
        {
            created += Create(adminEmail, "管理者", options.Admin.Password!, now, isAdmin: true);
        }
        else if (admin.Role != UserRole.Admin)
        {
            // 不偷偷升權：這是安全決定，要人明確執行 promote-admin
            throw new InvalidOperationException(
                $"{adminEmail} 已存在且不是管理者。請改用 `dotnet run --project src/Ticketing.Api -- promote-admin {adminEmail}`。");
        }

        if (options.PresetUsers.Enabled)
        {
            foreach (var preset in presets.Users)
            {
                var email = Normalize(preset.Email);
                if (await users.GetByEmailAsync(email, ct) is not null) continue;   // 已存在就跳過，不動它

                created += Create(email, preset.DisplayName, options.PresetUsers.Password!, now,
                                  isAdmin: string.Equals(preset.Role, nameof(UserRole.Admin), StringComparison.Ordinal));
            }
        }

        return created;
    }

    private int Create(string email, string displayName, string password, DateTimeOffset now, bool isAdmin)
    {
        var user = AppUser.CreateWithPassword(Guid.NewGuid(), email, displayName, now);
        user.SetPasswordHash(hasher.Hash(user, password));      // 存的是雜湊，不是密碼
        if (isAdmin) user.PromoteToAdmin();

        db.AppUsers.Add(user);
        return 1;
    }

    /// <summary>Email 正規化規則與 AuthService 一致：去空白、轉小寫。</summary>
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
