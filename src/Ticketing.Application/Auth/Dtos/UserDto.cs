using Ticketing.Domain.Users;

namespace Ticketing.Application.Auth.Dtos;

/// <summary>
/// 對外的使用者資料。**只有這四個欄位**——
/// <c>PasswordHash</c> 與 <c>GoogleSubject</c> 永遠不出現在任何回應裡。
///
/// 「不外洩」不是靠自律，是靠 <see cref="AuthMapper"/> 的 RMG020：
/// 來源多出來的欄位沒有明確忽略就編譯失敗（設計文件 13 第 2 節）。
/// </summary>
public sealed record UserDto(Guid Id, string Email, string DisplayName, UserRole Role);
