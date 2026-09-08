using Microsoft.Extensions.Logging;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Auth.Dtos;
using Ticketing.Domain.Common;
using Ticketing.Domain.Users;

namespace Ticketing.Application.Auth;

/// <summary>
/// 註冊、登入、Google 登入的流程。
///
/// 這個類別**沒有任何一行外部套件的程式碼**：密碼演算法、JWT、Google 驗簽
/// 各自躲在三個介面後面（設計文件 05 第 2 節）。所以 Application.Tests
/// 可以不連 Google、不算 PBKDF2，就測完整條流程。
///
/// 註冊與登入不需要 <c>ExecuteInTransactionAsync</c>：每條路徑只有一次
/// <c>SaveChangesAsync</c>，EF 本身就把它包成一個交易。
/// </summary>
public sealed class AuthService(
    IUserRepository users,
    IUnitOfWork uow,
    IPasswordHasher hasher,
    IJwtTokenIssuer jwt,
    IGoogleTokenVerifier google,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = Normalize(request.Email);

        // 先查只是為了給出好訊息；真正的保證是 UQ_AppUsers_Email。
        // 兩個請求同時通過這一行是正常的，下面的 catch 才是最終防線。
        if (await users.EmailExistsAsync(email, ct))
            throw new BookingRuleException(ErrorCode.EmailAlreadyRegistered, "此 Email 已註冊");

        var user = AppUser.CreateWithPassword(Guid.NewGuid(), email, request.DisplayName.Trim(), clock.GetUtcNow());
        user.SetPasswordHash(hasher.Hash(user, request.Password));   // 存的是雜湊，不是密碼
        users.Add(user);

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (UserIdentityConflictException conflict) when (conflict.Conflict == UserIdentityConflict.Email)
        {
            // 併發註冊：兩個請求都通過了上面的 EmailExists，其中一個撞唯一索引
            logger.LogInformation("RegisterRaceLost {Email}", email);
            throw new BookingRuleException(ErrorCode.EmailAlreadyRegistered, "此 Email 已註冊");
        }

        logger.LogInformation("UserRegistered {UserId}", user.Id);
        return Respond(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await users.GetByEmailAsync(Normalize(request.Email), ct);

        // 帳號不存在、是 Google 帳號（沒有密碼）、密碼錯——**回同一個訊息**。
        // 分開講等於送給攻擊者一支帳號列舉工具（教學 25 錯誤 3）。
        if (user?.PasswordHash is null || !hasher.Verify(user, user.PasswordHash, request.Password))
            throw new BookingRuleException(ErrorCode.Unauthorized, "Email 或密碼錯誤");

        return Respond(user);
    }

    public async Task<AuthResponse> GoogleAsync(GoogleLoginRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var identity = await google.VerifyAsync(request.IdToken, ct);   // 驗不過就丟 Unauthorized

        var user = await users.GetByGoogleSubjectAsync(identity.Subject, ct);
        if (user is not null) return Respond(user);                      // 第二次以後的登入

        var email = Normalize(identity.Email);
        if (await users.EmailExistsAsync(email, ct))
        {
            // ⚠️ 這裡有一個很窄但真實的時序空窗，是整合測試在滿載時抓到的：
            // 剛剛查 subject 的那一刻另一個併發的初次登入還沒 commit（所以查不到），
            // 等我們查 Email 時它已經 commit 了（所以查得到）。
            // 直接回 409 的話，同一個人的兩次登入其中一次會被誤判成「Email 被別人占用」。
            //
            // 兩次查的是同一列，所以 Email 說它存在時，再查一次 subject 一定看得到——
            // 這一行把空窗關掉。
            var raced = await users.GetByGoogleSubjectAsync(identity.Subject, ct);
            if (raced is not null)
            {
                logger.LogInformation("GoogleFirstLoginRaceLost {UserId}", raced.Id);
                return Respond(raced);
            }

            throw new BookingRuleException(ErrorCode.EmailAlreadyRegistered,
                                           "此 Email 已有帳號，請使用原本的登入方式");
        }

        user = AppUser.CreateWithGoogle(Guid.NewGuid(), email, identity.DisplayName, identity.Subject,
                                        clock.GetUtcNow());
        users.Add(user);

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (UserIdentityConflictException conflict)
        {
            // 解不開就用 `throw;` 原地重拋，保留原始堆疊——
            // 在別的方法裡 `throw conflict;` 會把堆疊重設，查問題時看不到真正的來源。
            user = await ResolveGoogleConflictAsync(identity, email, conflict, ct);
            if (user is null) throw;
        }

        logger.LogInformation("GoogleUserLinked {UserId}", user.Id);
        return Respond(user);
    }

    public async Task<UserDto> GetMeAsync(Guid userId, CancellationToken ct)
    {
        // token 有效但帳號已被刪除：這是「請重新登入」，不是 404——
        // 不對外承認或否認某個 id 存在。
        var user = await users.GetByIdAsync(userId, ct)
            ?? throw new BookingRuleException(ErrorCode.Unauthorized, "請重新登入");

        return AuthMapper.ToDto(user);
    }

    /// <summary>
    /// 兩個併發的 Google 初次登入，其中一個撞了索引。
    ///
    /// 關鍵是**用已驗證的 subject 重讀**，而不是看 SQL 錯誤訊息猜：
    /// 找到同一個人就當成功（他本來就是那個帳號），只有 Email 被別的身份占住才是真衝突。
    /// 重讀不到又沒人占用 Email 時回 <c>null</c>，由呼叫端**不假裝成功**地把原例外重拋成 500，
    /// 讓它變成一個看得見的問題，而不是悄悄回一個對不上的 token（設計文件 05 第 4.1 節）。
    /// </summary>
    private async Task<AppUser?> ResolveGoogleConflictAsync(GoogleIdentity identity, string email,
                                                           UserIdentityConflictException conflict,
                                                           CancellationToken ct)
    {
        var existing = await users.GetByGoogleSubjectAsync(identity.Subject, ct);
        if (existing is not null)
        {
            logger.LogInformation("GoogleFirstLoginRaceLost {UserId}", existing.Id);
            return existing;
        }

        if (conflict.Conflict == UserIdentityConflict.Email || await users.EmailExistsAsync(email, ct))
            throw new BookingRuleException(ErrorCode.EmailAlreadyRegistered,
                                           "此 Email 已有帳號，請使用原本的登入方式");

        logger.LogError("GoogleConflictUnresolved {Conflict}", conflict.Conflict);
        return null;
    }

    private AuthResponse Respond(AppUser user) => new(jwt.Issue(user), AuthMapper.ToDto(user));

    /// <summary>Email 正規化規則與 <c>UserSeeder</c> 一致：去空白、轉小寫。</summary>
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
