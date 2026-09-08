using Ticketing.Application.Auth;
using Ticketing.Application.Auth.Dtos;
using Ticketing.Domain.Users;

namespace Ticketing.Application.Tests;

/// <summary>
/// M01-Auth：實際跑一次對應，逐欄位比對值，並釘住「DTO 只有這四個欄位」。
///
/// 為什麼值得測一個由 source generator 產生的方法？
/// 因為我們要驗的不是 Mapperly 會不會複製欄位（它會），
/// 而是**我們有沒有不小心把不該給的欄位放進 DTO**（設計文件 10 第 2.1 節）。
/// </summary>
public class AuthMapperTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_password_account_maps_to_the_four_public_fields()
    {
        var id = Guid.NewGuid();
        var user = AppUser.CreateWithPassword(id, "buyer@example.com", "買家", Now);
        user.SetPasswordHash("AQAAAAIAAYag-not-a-real-hash");

        var dto = AuthMapper.ToDto(user);

        Assert.Equal(id, dto.Id);
        Assert.Equal("buyer@example.com", dto.Email);
        Assert.Equal("買家", dto.DisplayName);
        Assert.Equal(UserRole.Customer, dto.Role);
    }

    [Fact]
    public void An_admin_keeps_its_role_so_the_frontend_can_show_the_admin_entry()
    {
        var user = AppUser.CreateWithPassword(Guid.NewGuid(), "admin@example.com", "管理者", Now);
        user.SetPasswordHash("hash");
        user.PromoteToAdmin();

        Assert.Equal(UserRole.Admin, AuthMapper.ToDto(user).Role);
    }

    [Fact]
    public void A_google_account_maps_without_leaking_its_subject()
    {
        var user = AppUser.CreateWithGoogle(Guid.NewGuid(), "g@example.com", "G 使用者", "112233445566", Now);

        var dto = AuthMapper.ToDto(user);

        Assert.Equal("g@example.com", dto.Email);
        Assert.DoesNotContain("112233445566", dto.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UserDto_exposes_exactly_four_fields()
    {
        // 這個測試是給「以後某個人」看的：想在 UserDto 加欄位，先過這一關。
        // 配合 AuthMapper 的 RMG020（來源欄位沒對應就編譯失敗），
        // 密碼雜湊與 Google subject 沒有辦法安靜地跑進 API 回應裡。
        var names = typeof(UserDto).GetProperties()
            .Select(p => p.Name)
            .Where(name => name != "EqualityContract")     // record 自帶的
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["DisplayName", "Email", "Id", "Role"], names);
    }
}
