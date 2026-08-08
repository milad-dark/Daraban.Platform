using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Platform.Common;
using Microsoft.AspNetCore.Identity;

namespace Daraban.Modules.Identity.Services.Auth;

/// <summary>
/// Orchestrates Register/Login/Refresh/Logout as plain methods -- no MediatR (Task 1.1).
/// This is also where the "check security for hacking" hardening actually lives; see the
/// inline notes at each decision point, and the summary in the Task 2.3 write-up for the
/// reasoning behind each one.
/// </summary>
public sealed class AuthService : IAuthService
{
    // Fixed lockout policy -- simple and predictable over exponential backoff, which needs
    // more state (attempt-timestamp history) for marginal benefit at this scale. Revisit if
    // credential-stuffing volume ever justifies the extra complexity.
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IUserRepository _users;
    private readonly IRefreshTokenService _refreshTokens;
    private readonly IJwtTokenService _jwtTokens;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(
        IUserRepository users, IRefreshTokenService refreshTokens, IJwtTokenService jwtTokens, IPasswordHasher<User> passwordHasher)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _jwtTokens = jwtTokens;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<AuthUserResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        // Username/email-taken IS disclosed here -- standard registration UX, and a
        // documented trade-off (see Task 2.3 write-up): fully enumeration-resistant
        // registration needs the response to look identical either way, which in turn
        // needs real email delivery ("you already have an account" sent to the address
        // instead of shown in the API response) -- not possible yet, Notifications module
        // doesn't exist. Revisit once it does if this trade-off matters for your threat model.
        if (await _users.ExistsByUsernameAsync(request.Username, ct))
            return Result.Failure<AuthUserResponse>(new Error("IDENTITY.USERNAME_TAKEN", "That username is already in use.", ErrorType.Conflict));
        if (await _users.ExistsByEmailAsync(request.Email, ct))
            return Result.Failure<AuthUserResponse>(new Error("IDENTITY.EMAIL_TAKEN", "That email is already registered.", ErrorType.Conflict));

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(),
            IsActive = true,
            EmailConfirmed = false, // TODO: wire to a real confirmation email once Notifications exists
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        // PasswordHasher<T> = PBKDF2-HMAC-SHA256, 100k+ iterations, random per-user salt,
        // via Microsoft.Extensions.Identity.Core -- never store/compare plaintext.
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);

        return Result.Success(new AuthUserResponse(user.Id, user.Username, user.Email, user.DisplayName, user.DefaultEntityId ?? Guid.Empty));
    }

    public async Task<Result<AuthResult>> LoginAsync(LoginRequest request, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var genericFailure = Result.Failure<AuthResult>(
            new Error("IDENTITY.INVALID_CREDENTIALS", "Invalid username or password.", ErrorType.Validation));

        var user = await _users.GetByUsernameOrEmailAsync(request.UsernameOrEmail, ct);

        if (user is null)
        {
            // Timing-attack / enumeration mitigation: run the same PBKDF2 work even when
            // there's no account to check against, so "unknown user" and "wrong password"
            // take statistically indistinguishable time and return an identical message.
            _passwordHasher.VerifyHashedPassword(new User(), DummyHashForTimingParity, request.Password);
            return genericFailure;
        }

        if (user.LockoutEndAt is { } lockoutEnd && lockoutEnd > DateTimeOffset.UtcNow)
            return Result.Failure<AuthResult>(new Error("IDENTITY.ACCOUNT_LOCKED", "Too many failed attempts. Try again later.", ErrorType.Forbidden));

        if (!user.IsActive)
            return Result.Failure<AuthResult>(new Error("IDENTITY.ACCOUNT_DISABLED", "This account has been disabled.", ErrorType.Forbidden));

        var verifyResult = user.PasswordHash is null
            ? PasswordVerificationResult.Failed
            : _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verifyResult == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
                user.LockoutEndAt = DateTimeOffset.UtcNow.Add(LockoutDuration);
            await _users.SaveChangesAsync(ct);
            return genericFailure;
        }

        // ASP.NET Core's hasher signals when a hash was produced with older-but-still-valid
        // parameters; re-hashing on successful login keeps everyone migrated to current
        // params without a forced reset.
        if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        await _users.SaveChangesAsync(ct);

        return Result.Success(await IssueTokensAsync(user, ip, userAgent, ct));
    }

    public async Task<Result<AuthResult>> RefreshAsync(string presentedRefreshToken, CancellationToken ct = default)
    {
        var rotated = await _refreshTokens.ValidateAndRotateAsync(presentedRefreshToken, ct);
        if (rotated is null)
            return Result.Failure<AuthResult>(new Error("IDENTITY.REFRESH_TOKEN_INVALID", "Session expired -- please log in again.", ErrorType.Forbidden));

        var user = await _users.GetByIdAsync(rotated.Value.UserId, ct);
        // Re-check state at refresh time too, not just at login -- an account disabled or
        // force-logged-out (token_version bumped) between login and this refresh should not
        // silently keep working.
        if (user is null || !user.IsActive)
            return Result.Failure<AuthResult>(new Error("IDENTITY.REFRESH_TOKEN_INVALID", "Session expired -- please log in again.", ErrorType.Forbidden));

        var (accessToken, expiresAt) = _jwtTokens.IssueAccessToken(user, user.DefaultEntityId ?? Guid.Empty);
        return Result.Success(new AuthResult(accessToken, expiresAt, rotated.Value.NewToken, ToUserResponse(user)));
    }

    public Task LogoutAsync(string presentedRefreshToken, CancellationToken ct = default)
        => _refreshTokens.RevokeAsync(presentedRefreshToken, ct);

    private async Task<AuthResult> IssueTokensAsync(User user, string? ip, string? userAgent, CancellationToken ct)
    {
        var (accessToken, expiresAt) = _jwtTokens.IssueAccessToken(user, user.DefaultEntityId ?? Guid.Empty);
        var refreshToken = await _refreshTokens.IssueAsync(user.Id, ip, userAgent, ct);
        return new AuthResult(accessToken, expiresAt, refreshToken, ToUserResponse(user));
    }

    private static AuthUserResponse ToUserResponse(User u) => new(u.Id, u.Username, u.Email, u.DisplayName, u.DefaultEntityId ?? Guid.Empty);

    // A syntactically-valid PasswordHasher<T> hash for a password nobody will ever type,
    // used only to burn equivalent CPU time when no real account exists to compare against
    // (see LoginAsync). Generated once via the real hasher rather than hand-crafted, so it's
    // guaranteed to parse the same way a genuine stored hash would -- a hand-rolled fake
    // string risks the hasher's internal format decoder choking on it unpredictably instead
    // of cleanly returning Failed.
    private static readonly string DummyHashForTimingParity =
        new PasswordHasher<User>().HashPassword(new User(), "dummy-password-never-used-for-real-auth");
}
