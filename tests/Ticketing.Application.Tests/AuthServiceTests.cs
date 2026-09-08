using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Auth;
using Ticketing.Application.Auth.Dtos;
using Ticketing.Domain.Common;
using Ticketing.Domain.Users;

namespace Ticketing.Application.Tests;

/// <summary>
/// A05 與登入流程。**一個資料庫都沒有、一次 PBKDF2 都沒算、一次 Google 都沒連**——
/// 三個外部套件全部躲在介面後面，所以這一層測的是「流程對不對」，
/// 資料對不對交給 Api 層的真 SQL 測試（設計文件 10 第 2.1 節）。
/// </summary>
public class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenIssuer _jwt = Substitute.For<IJwtTokenIssuer>();
    private readonly IGoogleTokenVerifier _google = Substitute.For<IGoogleTokenVerifier>();
    private readonly FakeTimeProvider _clock = new(Now);

    public AuthServiceTests()
    {
        // 替身預設回空字串，會讓 SetPasswordHash 擋下來。
        // 這裡給的是「隨便但合法」的值——本層不在乎雜湊長什麼樣，
        // 個別測試要斷言時再自己覆寫。
        _hasher.Hash(Arg.Any<AppUser>(), Arg.Any<string>()).Returns("HASHED");
        _jwt.Issue(Arg.Any<AppUser>()).Returns("token");
    }

    private AuthService Service() =>
        new(_users, _uow, _hasher, _jwt, _google, _clock, NullLogger<AuthService>.Instance);

    // ── 註冊 ──────────────────────────────────────────────────────────

    [Fact] // A05
    public async Task Registering_an_existing_email_is_rejected_before_anything_is_written()
    {
        _users.EmailExistsAsync("taken@example.com", Arg.Any<CancellationToken>()).Returns(true);

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            Service().RegisterAsync(Register("taken@example.com"), CancellationToken.None));

        Assert.Equal(ErrorCode.EmailAlreadyRegistered, error.Code);

        // 沒有寫入，也沒有白算一次昂貴的雜湊
        _users.DidNotReceive().Add(Arg.Any<AppUser>());
        _hasher.DidNotReceive().Hash(Arg.Any<AppUser>(), Arg.Any<string>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registering_stores_the_hash_and_never_the_password()
    {
        _hasher.Hash(Arg.Any<AppUser>(), "correct horse battery").Returns("HASHED");

        var response = await Service().RegisterAsync(
            Register("new@example.com", password: "correct horse battery"), CancellationToken.None);

        var saved = _users.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IUserRepository.Add))
            .GetArguments()[0] as AppUser;

        Assert.NotNull(saved);
        Assert.Equal("HASHED", saved.PasswordHash);
        Assert.DoesNotContain("correct horse battery", saved.PasswordHash);
        Assert.Equal(Now, saved.CreatedAtUtc);
        Assert.Equal(UserRole.Customer, saved.Role);      // 沒有「第一個註冊的就是管理者」
        Assert.Equal("token", response.AccessToken);
    }

    [Theory]
    [InlineData("  Mixed@Example.COM  ")]
    [InlineData("mixed@example.com")]
    public async Task Email_is_trimmed_and_lower_cased_before_anything_else(string input)
    {
        await Service().RegisterAsync(Register(input), CancellationToken.None);

        await _users.Received().EmailExistsAsync("mixed@example.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_concurrent_registration_that_loses_the_unique_index_still_gets_409()
    {
        // 兩個請求都通過了 EmailExists，其中一個在 SaveChanges 撞索引——
        // 先查只是為了訊息好看，唯一索引才是保證。
        _users.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Throws(new UserIdentityConflictException(UserIdentityConflict.Email, new InvalidOperationException()));

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            Service().RegisterAsync(Register("race@example.com"), CancellationToken.None));

        Assert.Equal(ErrorCode.EmailAlreadyRegistered, error.Code);
    }

    // ── 登入 ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Unknown_email_and_wrong_password_fail_with_the_exact_same_message()
    {
        _users.GetByEmailAsync("nobody@example.com", Arg.Any<CancellationToken>()).Returns((AppUser?)null);

        var existing = PasswordUser("someone@example.com");
        _users.GetByEmailAsync("someone@example.com", Arg.Any<CancellationToken>()).Returns(existing);
        _hasher.Verify(existing, Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var unknown = await Assert.ThrowsAsync<BookingRuleException>(() =>
            Service().LoginAsync(Login("nobody@example.com"), CancellationToken.None));
        var wrongPassword = await Assert.ThrowsAsync<BookingRuleException>(() =>
            Service().LoginAsync(Login("someone@example.com"), CancellationToken.None));

        // 分開講等於送給攻擊者一支帳號列舉工具
        Assert.Equal(ErrorCode.Unauthorized, unknown.Code);
        Assert.Equal(unknown.Code, wrongPassword.Code);
        Assert.Equal(unknown.Message, wrongPassword.Message);
    }

    [Fact]
    public async Task A_google_account_cannot_be_logged_into_with_a_password()
    {
        // PasswordHash 是 null：這種帳號沒有密碼可比對，連 Verify 都不該被呼叫
        var googleUser = AppUser.CreateWithGoogle(Guid.NewGuid(), "g@example.com", "G", "sub-1", Now);
        _users.GetByEmailAsync("g@example.com", Arg.Any<CancellationToken>()).Returns(googleUser);

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            Service().LoginAsync(Login("g@example.com"), CancellationToken.None));

        Assert.Equal(ErrorCode.Unauthorized, error.Code);
        _hasher.DidNotReceive().Verify(Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task A_correct_password_returns_a_token_and_the_public_user_fields()
    {
        var user = PasswordUser("ok@example.com");
        _users.GetByEmailAsync("ok@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(user, "HASHED", "pw").Returns(true);
        _jwt.Issue(user).Returns("issued-token");

        var response = await Service().LoginAsync(Login("ok@example.com", "pw"), CancellationToken.None);

        Assert.Equal("issued-token", response.AccessToken);
        Assert.Equal(user.Id, response.User.Id);
        Assert.Equal("ok@example.com", response.User.Email);
    }

    // ── Google ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_first_google_login_creates_the_account()
    {
        VerifiesAs(new GoogleIdentity("sub-1", "New@Example.com", "新使用者"));
        _users.GetByGoogleSubjectAsync("sub-1", Arg.Any<CancellationToken>()).Returns((AppUser?)null);
        _users.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await Service().GoogleAsync(new GoogleLoginRequest { IdToken = "id-token" }, CancellationToken.None);

        var created = (AppUser)_users.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IUserRepository.Add)).GetArguments()[0]!;

        Assert.Equal("sub-1", created.GoogleSubject);
        Assert.Equal("new@example.com", created.Email);      // 一樣正規化
        Assert.Null(created.PasswordHash);                   // Google 帳號沒有密碼
    }

    [Fact]
    public async Task A_returning_google_user_is_matched_by_subject_and_nothing_is_created()
    {
        var existing = AppUser.CreateWithGoogle(Guid.NewGuid(), "g@example.com", "G", "sub-1", Now);
        VerifiesAs(new GoogleIdentity("sub-1", "changed@example.com", "改過名字"));
        _users.GetByGoogleSubjectAsync("sub-1", Arg.Any<CancellationToken>()).Returns(existing);

        var response = await Service().GoogleAsync(new GoogleLoginRequest { IdToken = "t" },
                                                   CancellationToken.None);

        // Google 那邊改了 Email 或顯示名，仍然是同一個人：身份主鍵是 subject
        Assert.Equal(existing.Id, response.User.Id);
        Assert.Equal("g@example.com", response.User.Email);
        _users.DidNotReceive().Add(Arg.Any<AppUser>());
    }

    [Fact]
    public async Task Google_refuses_to_take_over_an_email_that_already_has_a_password_account()
    {
        VerifiesAs(new GoogleIdentity("sub-9", "taken@example.com", "冒名者"));
        _users.GetByGoogleSubjectAsync("sub-9", Arg.Any<CancellationToken>()).Returns((AppUser?)null);
        _users.EmailExistsAsync("taken@example.com", Arg.Any<CancellationToken>()).Returns(true);

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            Service().GoogleAsync(new GoogleLoginRequest { IdToken = "t" }, CancellationToken.None));

        Assert.Equal(ErrorCode.EmailAlreadyRegistered, error.Code);
        _users.DidNotReceive().Add(Arg.Any<AppUser>());
    }

    [Fact]
    public async Task Two_simultaneous_first_google_logins_end_up_on_one_account()
    {
        var winner = AppUser.CreateWithGoogle(Guid.NewGuid(), "g@example.com", "G", "sub-1", Now);
        VerifiesAs(new GoogleIdentity("sub-1", "g@example.com", "G"));

        // 第一次查沒有（所以我們決定建立），SaveChanges 撞索引，重查就找得到了
        _users.GetByGoogleSubjectAsync("sub-1", Arg.Any<CancellationToken>())
              .Returns(_ => null, _ => winner);
        _users.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Throws(
            new UserIdentityConflictException(UserIdentityConflict.GoogleSubject, new InvalidOperationException()));

        var response = await Service().GoogleAsync(new GoogleLoginRequest { IdToken = "t" },
                                                   CancellationToken.None);

        Assert.Equal(winner.Id, response.User.Id);      // 輸的那一邊也拿到同一個帳號
    }

    [Fact]
    public async Task An_unexplainable_conflict_is_reported_instead_of_faked_into_a_success()
    {
        VerifiesAs(new GoogleIdentity("sub-1", "g@example.com", "G"));
        _users.GetByGoogleSubjectAsync("sub-1", Arg.Any<CancellationToken>()).Returns((AppUser?)null);
        _users.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Throws(
            new UserIdentityConflictException(UserIdentityConflict.GoogleSubject, new InvalidOperationException()));

        // 撞了索引、重讀卻找不到、Email 也沒被占用——這是我們沒想到的情況。
        // 回一個對不上的 token 比回 500 危險得多。
        await Assert.ThrowsAsync<UserIdentityConflictException>(() =>
            Service().GoogleAsync(new GoogleLoginRequest { IdToken = "t" }, CancellationToken.None));
    }

    // ── /me ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_valid_token_for_a_deleted_account_is_asked_to_log_in_again()
    {
        _users.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((AppUser?)null);

        var error = await Assert.ThrowsAsync<BookingRuleException>(() =>
            Service().GetMeAsync(Guid.NewGuid(), CancellationToken.None));

        // 不是 404：不對外承認或否認某個 id 存在
        Assert.Equal(ErrorCode.Unauthorized, error.Code);
    }

    // ── helpers ───────────────────────────────────────────────────────

    private void VerifiesAs(GoogleIdentity identity) =>
        _google.VerifyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(identity);

    private static RegisterRequest Register(string email, string password = "password123") =>
        new() { Email = email, Password = password, DisplayName = "測試使用者" };

    private static LoginRequest Login(string email, string password = "password123") =>
        new() { Email = email, Password = password };

    private static AppUser PasswordUser(string email)
    {
        var user = AppUser.CreateWithPassword(Guid.NewGuid(), email, "測試使用者", Now);
        user.SetPasswordHash("HASHED");
        return user;
    }
}
