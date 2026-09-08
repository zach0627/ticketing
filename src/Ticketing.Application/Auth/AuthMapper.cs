using Riok.Mapperly.Abstractions;
using Ticketing.Application.Auth.Dtos;
using Ticketing.Domain.Users;

namespace Ticketing.Application.Auth;

/// <summary>
/// <see cref="AppUser"/> → <see cref="UserDto"/>。
///
/// 三個 <c>MapperIgnoreSource</c> 是這個對應器最重要的部分：
/// <c>RMG020</c>（來源欄位沒有被對應）在本專案設成 **error**，
/// 所以「不外洩密碼雜湊」不是靠 code review，是靠編譯器——
/// 想偷偷把 <c>PasswordHash</c> 加進 DTO，或想刪掉這幾行，建置都會失敗
/// （設計文件 12 ADR-10）。
/// </summary>
[Mapper]
public static partial class AuthMapper
{
    [MapperIgnoreSource(nameof(AppUser.PasswordHash))]
    [MapperIgnoreSource(nameof(AppUser.GoogleSubject))]
    [MapperIgnoreSource(nameof(AppUser.CreatedAtUtc))]
    public static partial UserDto ToDto(AppUser user);
}
