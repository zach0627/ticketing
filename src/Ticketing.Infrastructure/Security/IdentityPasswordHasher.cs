using Microsoft.AspNetCore.Identity;
using Ticketing.Application.Abstractions;
using Ticketing.Domain.Users;

namespace Ticketing.Infrastructure.Security;

/// <summary>
/// 包 <see cref="PasswordHasher{TUser}"/>。密碼演算法（PBKDF2、疊代次數、加鹽）
/// 交給成熟套件處理，我們不自己發明。無狀態，註冊為 Singleton。
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<AppUser> _inner = new();

    public string Hash(AppUser user, string password) => _inner.HashPassword(user, password);

    /// <summary>
    /// 需要重新雜湊時（套件升級了疊代次數）目前一律當成功處理，不強制使用者改密碼；
    /// 要做無縫升級時再於此回寫新雜湊。
    /// </summary>
    public bool Verify(AppUser user, string hashedPassword, string providedPassword)
        => _inner.VerifyHashedPassword(user, hashedPassword, providedPassword)
           is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
