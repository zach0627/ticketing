using Ticketing.Domain.Users;

namespace Ticketing.Application.Abstractions;

/// <summary>
/// 隔開 <c>Microsoft.AspNetCore.Identity</c>：Application 只知道「雜湊」與「驗證」兩件事，
/// 換演算法不影響使用案例（設計文件 05）。
/// </summary>
public interface IPasswordHasher
{
    string Hash(AppUser user, string password);
    bool Verify(AppUser user, string hashedPassword, string providedPassword);
}
