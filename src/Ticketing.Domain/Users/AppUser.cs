using Ticketing.Domain.Common;

namespace Ticketing.Domain.Users;

/// <summary>
/// 使用者。同一個 Email **不能同時有密碼與 Google 身份**：
/// 這是刻意的，避免「先用別人的 Email 註冊密碼帳號，再等本人用 Google 登入被合併」的接管風險
/// （設計文件 05 第 3 節）。
/// </summary>
public sealed class AppUser
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = "";         // 由 Application 正規化後傳入
    public string DisplayName { get; private set; } = "";
    public string? PasswordHash { get; private set; }       // Email 註冊才有；存的是雜湊，不是密碼
    public string? GoogleSubject { get; private set; }      // Google 的 sub，不用 Email 當身份主鍵
    public UserRole Role { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private AppUser() { }                                   // EF Core 用

    /// <summary>
    /// Email／密碼註冊。密碼雜湊要另外呼叫 <see cref="SetPasswordHash"/> 設定——
    /// 因為雜湊器需要 user 物件本身，這是 <c>PasswordHasher&lt;TUser&gt;</c> 的介面形狀。
    /// 「至少有一種身份」的最終保證在資料庫 CHECK（設計文件 04 第 3 節）。
    /// </summary>
    public static AppUser CreateWithPassword(Guid id, string normalizedEmail, string displayName, DateTimeOffset now)
    {
        EnsureIdentityFields(id, normalizedEmail, displayName);

        return new AppUser
        {
            Id = id,
            Email = normalizedEmail,
            DisplayName = displayName,
            Role = UserRole.Customer,
            CreatedAtUtc = now
        };
    }

    public static AppUser CreateWithGoogle(Guid id, string normalizedEmail, string displayName,
                                           string googleSubject, DateTimeOffset now)
    {
        EnsureIdentityFields(id, normalizedEmail, displayName);

        if (string.IsNullOrWhiteSpace(googleSubject))
            throw new BookingRuleException(ErrorCode.ValidationFailed, "Google subject 不可為空");

        return new AppUser
        {
            Id = id,
            Email = normalizedEmail,
            DisplayName = displayName,
            GoogleSubject = googleSubject,
            Role = UserRole.Customer,
            CreatedAtUtc = now
        };
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new BookingRuleException(ErrorCode.ValidationFailed, "密碼雜湊不可為空");
        if (GoogleSubject is not null)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "Google 帳號不可再設定密碼");

        PasswordHash = passwordHash;
    }

    /// <summary>只能由 seed 或 <c>dotnet run -- promote-admin</c> 呼叫，沒有公開的升權 API。</summary>
    public void PromoteToAdmin() => Role = UserRole.Admin;

    private static void EnsureIdentityFields(Guid id, string normalizedEmail, string displayName)
    {
        if (id == Guid.Empty)
            throw new BookingRuleException(ErrorCode.ValidationFailed, "使用者 Id 不可為空");
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            throw new BookingRuleException(ErrorCode.ValidationFailed, "Email 不可為空");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new BookingRuleException(ErrorCode.ValidationFailed, "顯示名稱不可為空");
    }
}
