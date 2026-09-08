using System.Security.Claims;
using Ticketing.Application.Abstractions;
using Ticketing.Domain.Common;
using Ticketing.Domain.Users;

namespace Ticketing.Api.Auth;

/// <summary>
/// 「這次請求是誰」。**只存在於 Api 層**——
/// Service 的簽章直接收 <c>Guid buyerId</c>，所以 Application 從頭到尾不認識 <c>HttpContext</c>
/// （設計文件 05 第 4.3 節）。
///
/// Scoped：一個請求一份。
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    /// <summary>
    /// JWT 的 <c>sub</c>。走到這裡時 <c>[Authorize]</c> 與 <c>OnTokenValidated</c>
    /// 已經確認過它是合法 Guid，這個例外是最後一道防線，不是正常流程。
    /// </summary>
    public Guid Id => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(JwtClaims.Subject), out var id)
        ? id
        : throw new BookingRuleException(ErrorCode.Unauthorized, "請重新登入");

    public bool IsAdmin => accessor.HttpContext?.User.IsInRole(nameof(UserRole.Admin)) ?? false;
}
