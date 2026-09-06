using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Modules.Identity.Services.Auth;
using Daraban.Platform.Common;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace Daraban.Modules.Identity.Tests;

/// <summary>
/// AuthService with repositories and the token services mocked. The password hasher is the REAL
/// <see cref="PasswordHasher{T}"/> rather than a mock -- these tests are about whether the
/// credential checks actually hold, and a mocked hasher that always returns Success would prove
/// nothing about that.
/// </summary>
public class AuthServiceTests
{
    private const string GoodPassword = "correct-horse-battery-staple";
    private const string WrongPassword = "wrong-horse-battery-staple";

    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);
    private readonly Mock<IRefreshTokenService> _refreshTokens = new(MockBehavior.Strict);
    private readonly Mock<IJwtTokenService> _jwtTokens = new(MockBehavior.Strict);
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();

    private AuthService CreateSut() =>
        new(_users.Object, _refreshTokens.Object, _jwtTokens.Object, _hasher);

    private User UserWith(
        string password = GoodPassword,
        bool isActive = true,
        int failedLoginCount = 0,
        DateTimeOffset? lockoutEndAt = null,
        bool hashPassword = true)
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Username = "alice",
            Email = "alice@example.com",
            DisplayName = "Alice",
            IsActive = isActive,
            FailedLoginCount = failedLoginCount,
            LockoutEndAt = lockoutEndAt,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        if (hashPassword)
            user.PasswordHash = _hasher.HashPassword(user, password);

        return user;
    }

    /// <summary>Arranges a successful token issuance for a login that gets that far.</summary>
    private void ArrangeTokenIssuance(string refreshToken = "refresh-token-value")
    {
        _jwtTokens.Setup(t => t.IssueAccessToken(It.IsAny<User>(), It.IsAny<Guid>()))
            .Returns(("access-token-value", DateTimeOffset.UtcNow.AddMinutes(15)));
        _refreshTokens.Setup(t => t.IssueAsync(
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refreshToken);
    }

    // ---- Register ----------------------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_Rejects_A_Taken_Username()
    {
        _users.Setup(r => r.ExistsByUsernameAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().RegisterAsync(
            new RegisterRequest("alice", "alice@example.com", GoodPassword, "Alice"));

        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.USERNAME_TAKEN", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_Rejects_A_Taken_Email()
    {
        _users.Setup(r => r.ExistsByUsernameAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.ExistsByEmailAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().RegisterAsync(
            new RegisterRequest("alice", "alice@example.com", GoodPassword, "Alice"));

        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.EMAIL_TAKEN", result.Error!.Code);
    }

    [Fact]
    public async Task RegisterAsync_Checks_Uniqueness_Against_The_Trimmed_Values()
    {
        // The stored row is trimmed, so the check has to use the trimmed value too. Checking the
        // raw input meant " alice" passed its own check and then collided with "alice" on the
        // unique index -- a 500 instead of a 409.
        _users.Setup(r => r.ExistsByUsernameAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().RegisterAsync(
            new RegisterRequest("  alice  ", " alice@example.com ", GoodPassword, " Alice "));

        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.USERNAME_TAKEN", result.Error!.Code);
        _users.Verify(r => r.ExistsByUsernameAsync("alice", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_Never_Stores_The_Plaintext_Password()
    {
        User? captured = null;
        _users.Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u)
            .Returns(Task.CompletedTask);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().RegisterAsync(
            new RegisterRequest("alice", "alice@example.com", GoodPassword, "Alice"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured!.PasswordHash);
        Assert.DoesNotContain(GoodPassword, captured.PasswordHash);

        // And the stored hash must actually verify -- proving it is a real PBKDF2 hash of that
        // password, not some placeholder.
        Assert.Equal(
            PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(captured, captured.PasswordHash!, GoodPassword));
    }

    [Fact]
    public async Task RegisterAsync_Trims_Stored_Values_And_Starts_Unconfirmed()
    {
        User? captured = null;
        _users.Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u)
            .Returns(Task.CompletedTask);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().RegisterAsync(
            new RegisterRequest("  alice  ", " alice@example.com ", GoodPassword, "  Alice  "));

        Assert.Equal("alice", captured!.Username);
        Assert.Equal("alice@example.com", captured.Email);
        Assert.Equal("Alice", captured.DisplayName);
        Assert.True(captured.IsActive);
        Assert.False(captured.EmailConfirmed);
        Assert.Equal(7, captured.Id.Version); // UUIDv7
    }

    [Fact]
    public async Task RegisterAsync_Does_Not_Issue_Tokens()
    {
        _users.Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().RegisterAsync(
            new RegisterRequest("alice", "alice@example.com", GoodPassword, "Alice"));

        // Registration returns a user, not a session -- the caller must log in. Strict mocks make
        // this airtight: any token call would throw.
        Assert.True(result.IsSuccess);
        _jwtTokens.VerifyNoOtherCalls();
        _refreshTokens.VerifyNoOtherCalls();
    }

    // ---- Login: credential checks -------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_Returns_The_Same_Generic_Error_For_Unknown_User_And_Wrong_Password()
    {
        // Unknown user.
        _users.Setup(r => r.GetByUsernameOrEmailAsync("ghost", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var unknown = await CreateSut().LoginAsync(new LoginRequest("ghost", WrongPassword), null, null);

        // Known user, wrong password.
        var user = UserWith();
        _users.Setup(r => r.GetByUsernameOrEmailAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var wrongPassword = await CreateSut().LoginAsync(new LoginRequest("alice", WrongPassword), null, null);

        // Identical code and message: anything else is a user-enumeration oracle.
        Assert.False(unknown.IsSuccess);
        Assert.False(wrongPassword.IsSuccess);
        Assert.Equal(unknown.Error!.Code, wrongPassword.Error!.Code);
        Assert.Equal(unknown.Error.Message, wrongPassword.Error.Message);
        Assert.Equal("IDENTITY.INVALID_CREDENTIALS", unknown.Error.Code);
    }

    [Fact]
    public async Task LoginAsync_Succeeds_With_The_Correct_Password()
    {
        var user = UserWith();
        _users.Setup(r => r.GetByUsernameOrEmailAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        ArrangeTokenIssuance();

        var result = await CreateSut().LoginAsync(new LoginRequest("alice", GoodPassword), "1.2.3.4", "UA");

        Assert.True(result.IsSuccess);
        Assert.Equal("access-token-value", result.Value.AccessToken);
        Assert.Equal("refresh-token-value", result.Value.RefreshToken);
        Assert.Equal(user.Id, result.Value.User.Id);
    }

    [Fact]
    public async Task LoginAsync_Rejects_A_User_With_No_Password_Hash()
    {
        // Admin-provisioned account that has never had a password set.
        var user = UserWith(hashPassword: false);
        _users.Setup(r => r.GetByUsernameOrEmailAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().LoginAsync(new LoginRequest("alice", GoodPassword), null, null);

        // Closed, not open: a null hash must never be treated as "no password required".
        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.INVALID_CREDENTIALS", result.Error!.Code);
    }

    [Fact]
    public async Task LoginAsync_Refuses_A_Disabled_Account_Before_Checking_The_Password()
    {
        var user = UserWith(isActive: false);
        _users.Setup(r => r.GetByUsernameOrEmailAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateSut().LoginAsync(new LoginRequest("alice", GoodPassword), null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.ACCOUNT_DISABLED", result.Error!.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);

        // No token issuance, and no failed-attempt bookkeeping either.
        _jwtTokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LoginAsync_Passes_The_Ip_And_UserAgent_To_The_Refresh_Token()
    {
        var user = UserWith();
        _users.Setup(r => r.GetByUsernameOrEmailAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        ArrangeTokenIssuance();

        await CreateSut().LoginAsync(new LoginRequest("alice", GoodPassword), "203.0.113.7", "Mozilla/5.0");

        _refreshTokens.Verify(t => t.IssueAsync(
            user.Id, "203.0.113.7", "Mozilla/5.0", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Login: lockout -----------------------------------------------------------------------

    [Fact]
    public async Task LoginAsync_Increments_The_Failure_Counter_On_A_Wrong_Password()
    {
        var user = UserWith(failedLoginCount: 2);
        _users.Setup(r => r.GetByUsernameOrEmailAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().LoginAsync(new LoginRequest("alice", WrongPassword), null, null);

        Assert.Equal(3, user.FailedLoginCount);
        Assert.Null(user.LockoutEndAt);
    }

    [Fact]
    public async Task LoginAsync_Locks_The_Account_On_The_Fifth_Consecutive_Failure()
    {
        var user = UserWith(failedLoginCount: 4);
        _users.Setup(r => r.GetByUsernameOrEmailAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().LoginAsync(new LoginRequest("alice", WrongPassword), null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(5, user.FailedLoginCount);
        Assert.NotNull(user.LockoutEndAt);
        Assert.True(user.LockoutEndAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_Refuses_A_Locked_Account_Even_With_The_Right_Password()
    {
        var user = UserWith(failedLoginCount: 5, lockoutEndAt: DateTimeOffset.UtcNow.AddMinutes(10));
        _users.Setup(r => r.GetByUsernameOrEmailAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateSut().LoginAsync(new LoginRequest("alice", GoodPassword), null, null);

        // The lockout is the whole point -- a correct password during the window must not bypass it,
        // otherwise a credential-stuffing run that eventually guesses right still wins.
        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.ACCOUNT_LOCKED", result.Error!.Code);
        _jwtTokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LoginAsync_Clears_The_Failure_Counter_When_An_Expired_Lockout_Elapses()
    {
        // Lockout has passed, but the counter is still parked at the threshold.
        var user = UserWith(failedLoginCount: 5, lockoutEndAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        _users.Setup(r => r.GetByUsernameOrEmailAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().LoginAsync(new LoginRequest("alice", WrongPassword), null, null);

        // Regression guard: the counter used to survive the expired lockout, so the very next wrong
        // password re-locked the account instantly -- turning a 15-minute lockout into a permanent
        // one for anyone who mistyped twice. It should now be 1, not 6.
        Assert.False(result.IsSuccess);
        Assert.Equal(1, user.FailedLoginCount);
        Assert.Null(user.LockoutEndAt);
    }

    [Fact]
    public async Task LoginAsync_Lets_A_User_Back_In_After_The_Lockout_Expires()
    {
        var user = UserWith(failedLoginCount: 5, lockoutEndAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        _users.Setup(r => r.GetByUsernameOrEmailAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        ArrangeTokenIssuance();

        var result = await CreateSut().LoginAsync(new LoginRequest("alice", GoodPassword), null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockoutEndAt);
    }

    [Fact]
    public async Task LoginAsync_Resets_The_Failure_Counter_On_Success()
    {
        var user = UserWith(failedLoginCount: 3);
        _users.Setup(r => r.GetByUsernameOrEmailAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        ArrangeTokenIssuance();

        await CreateSut().LoginAsync(new LoginRequest("alice", GoodPassword), null, null);

        Assert.Equal(0, user.FailedLoginCount);
    }

    // ---- Refresh -----------------------------------------------------------------------------

    [Fact]
    public async Task RefreshAsync_Fails_When_The_Token_Cannot_Be_Rotated()
    {
        _refreshTokens.Setup(t => t.ValidateAndRotateAsync("stale", It.IsAny<CancellationToken>()))
            .ReturnsAsync(((Guid, string)?)null);

        var result = await CreateSut().RefreshAsync("stale");

        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.REFRESH_TOKEN_INVALID", result.Error!.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task RefreshAsync_Issues_A_New_Access_Token_And_Returns_The_Rotated_Refresh_Token()
    {
        var user = UserWith();
        _refreshTokens.Setup(t => t.ValidateAndRotateAsync("old", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user.Id, "rotated-token"));
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _jwtTokens.Setup(t => t.IssueAccessToken(user, It.IsAny<Guid>()))
            .Returns(("fresh-access-token", DateTimeOffset.UtcNow.AddMinutes(15)));

        var result = await CreateSut().RefreshAsync("old");

        Assert.True(result.IsSuccess);
        Assert.Equal("fresh-access-token", result.Value.AccessToken);
        Assert.Equal("rotated-token", result.Value.RefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_Revokes_The_New_Token_When_The_Account_Was_Disabled_Mid_Session()
    {
        var user = UserWith(isActive: false);
        _refreshTokens.Setup(t => t.ValidateAndRotateAsync("old", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user.Id, "rotated-token"));
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _refreshTokens.Setup(t => t.RevokeAsync("rotated-token", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateSut().RefreshAsync("old");

        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.REFRESH_TOKEN_INVALID", result.Error!.Code);

        // Rotation has already minted a replacement token by this point. Leaving it alive would hand
        // a usable session to a disabled account.
        _refreshTokens.Verify(t => t.RevokeAsync("rotated-token", It.IsAny<CancellationToken>()), Times.Once);
        _jwtTokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RefreshAsync_Revokes_The_New_Token_When_The_User_Was_Soft_Deleted()
    {
        var user = UserWith();
        user.IsDeleted = true;

        _refreshTokens.Setup(t => t.ValidateAndRotateAsync("old", It.IsAny<CancellationToken>()))
            .ReturnsAsync((user.Id, "rotated-token"));
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _refreshTokens.Setup(t => t.RevokeAsync("rotated-token", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateSut().RefreshAsync("old");

        // Checked explicitly rather than relying on the DbContext query filter, so this holds even
        // if a caller hands over an unfiltered query.
        Assert.False(result.IsSuccess);
        _refreshTokens.Verify(t => t.RevokeAsync("rotated-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_Fails_When_The_User_No_Longer_Exists()
    {
        var userId = Guid.CreateVersion7();
        _refreshTokens.Setup(t => t.ValidateAndRotateAsync("old", It.IsAny<CancellationToken>()))
            .ReturnsAsync((userId, "rotated-token"));
        _users.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _refreshTokens.Setup(t => t.RevokeAsync("rotated-token", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateSut().RefreshAsync("old");

        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.REFRESH_TOKEN_INVALID", result.Error!.Code);
    }

    // ---- Logout ------------------------------------------------------------------------------

    [Fact]
    public async Task LogoutAsync_Delegates_To_The_Refresh_Token_Service()
    {
        _refreshTokens.Setup(t => t.RevokeAsync("presented", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateSut().LogoutAsync("presented");

        _refreshTokens.Verify(t => t.RevokeAsync("presented", It.IsAny<CancellationToken>()), Times.Once);
    }
}
