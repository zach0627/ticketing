using Ticketing.Domain.Common;
using Ticketing.Domain.Users;

namespace Ticketing.Domain.Tests;

public class AppUserTests
{
    private static readonly Guid Id = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void A_password_user_starts_as_a_customer_with_no_hash_yet()
    {
        var user = AppUser.CreateWithPassword(Id, "zach@example.com", "Zach", TestData.Now);

        Assert.Equal(UserRole.Customer, user.Role);
        Assert.Null(user.PasswordHash);          // 雜湊器需要 user 物件本身，所以分兩步
        Assert.Null(user.GoogleSubject);
        Assert.Equal(TestData.Now, user.CreatedAtUtc);
    }

    [Fact]
    public void Setting_the_password_hash_stores_the_hash_not_the_password()
    {
        var user = AppUser.CreateWithPassword(Id, "zach@example.com", "Zach", TestData.Now);

        user.SetPasswordHash("AQAAAAIAAYag...hashed");

        Assert.Equal("AQAAAAIAAYag...hashed", user.PasswordHash);
    }

    [Fact]
    public void A_google_user_is_identified_by_subject_not_email()
    {
        var user = AppUser.CreateWithGoogle(Id, "zach@example.com", "Zach", "google-sub-123", TestData.Now);

        Assert.Equal("google-sub-123", user.GoogleSubject);
        Assert.Null(user.PasswordHash);
    }

    [Fact]
    public void A_google_account_cannot_also_get_a_password()
    {
        // 防接管：不允許把 Google 帳號補上密碼，也不允許反向合併（設計文件 05 第 3 節）
        var user = AppUser.CreateWithGoogle(Id, "zach@example.com", "Zach", "google-sub-123", TestData.Now);

        var ex = Assert.Throws<BookingRuleException>(() => user.SetPasswordHash("whatever"));

        Assert.Equal(ErrorCode.ValidationFailed, ex.Code);
    }

    [Fact]
    public void An_empty_google_subject_is_rejected()
        => Assert.Throws<BookingRuleException>(() =>
            AppUser.CreateWithGoogle(Id, "zach@example.com", "Zach", "  ", TestData.Now));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_email_is_rejected(string email)
        => Assert.Throws<BookingRuleException>(() =>
            AppUser.CreateWithPassword(Id, email, "Zach", TestData.Now));

    [Fact]
    public void An_empty_display_name_is_rejected()
        => Assert.Throws<BookingRuleException>(() =>
            AppUser.CreateWithPassword(Id, "zach@example.com", " ", TestData.Now));

    [Fact]
    public void An_empty_id_is_rejected()
        => Assert.Throws<BookingRuleException>(() =>
            AppUser.CreateWithPassword(Guid.Empty, "zach@example.com", "Zach", TestData.Now));

    [Fact]
    public void An_empty_password_hash_is_rejected()
    {
        var user = AppUser.CreateWithPassword(Id, "zach@example.com", "Zach", TestData.Now);

        Assert.Throws<BookingRuleException>(() => user.SetPasswordHash(""));
    }

    [Fact]
    public void Promoting_to_admin_changes_only_the_role()
    {
        var user = AppUser.CreateWithPassword(Id, "admin@example.com", "Admin", TestData.Now);

        user.PromoteToAdmin();

        Assert.Equal(UserRole.Admin, user.Role);
        Assert.Equal("admin@example.com", user.Email);
    }
}
