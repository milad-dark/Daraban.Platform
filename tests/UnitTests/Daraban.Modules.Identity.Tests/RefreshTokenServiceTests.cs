using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Modules.Identity.Services.Auth;
using Daraban.Platform.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daraban.Modules.Identity.Tests;

/// <summary>
/// RefreshTokenService against a fake in-memory repository rather than a mock -- rotation is
/// stateful (revoke the old row, insert a new one in the same family, link them), and asserting
/// on Moq call sequences would test the choreography instead of the outcome.
/// </summary>
public class RefreshTokenServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeRefreshTokenRepository _repo = new();

    private RefreshTokenService CreateSut(int slidingDays = 14, int absoluteDays = 90)
        => new(_repo, Options.Create(new JwtOptions
        {
            RefreshTokenLifetimeDays = slidingDays,
            RefreshTokenAbsoluteLifetimeDays = absoluteDays,
        }));

    // ---- Issue -------------------------------------------------------------------------------

    [Fact]
    public async Task IssueAsync_Returns_A_High_Entropy_Url_Safe_Token()
    {
        var token = await CreateSut().IssueAsync(UserId, null, null);

        Assert.False(string.IsNullOrWhiteSpace(token));

        // Base64Url, not plain Base64: '+' and '/' get mangled in cookies, headers and URLs.
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);

        // 32 bytes -> 43 Base64 chars once padding is stripped.
        Assert.Equal(43, token.Length);
    }

    [Fact]
    public async Task IssueAsync_Never_Stores_The_Raw_Token()
    {
        var token = await CreateSut().IssueAsync(UserId, null, null);

        var stored = Assert.Single(_repo.Tokens);

        // A database leak alone must never yield a usable token.
        Assert.NotEqual(token, stored.TokenHash);
        Assert.DoesNotContain(token, stored.TokenHash);
    }

    [Fact]
    public async Task IssueAsync_Produces_A_Distinct_Token_Every_Time()
    {
        var sut = CreateSut();

        var first = await sut.IssueAsync(UserId, null, null);
        var second = await sut.IssueAsync(UserId, null, null);

        Assert.NotEqual(first, second);
        Assert.Equal(2, _repo.Tokens.Count);

        // Separate logins are separate sessions, so separate families.
        Assert.NotEqual(_repo.Tokens[0].FamilyId, _repo.Tokens[1].FamilyId);
    }

    [Fact]
    public async Task IssueAsync_Stamps_The_Family_Start_Time()
    {
        var before = DateTimeOffset.UtcNow;

        await CreateSut().IssueAsync(UserId, "1.2.3.4", "UA");

        var stored = Assert.Single(_repo.Tokens);
        Assert.InRange(stored.FamilyIssuedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal("1.2.3.4", stored.IssuedFromIp);
        Assert.Equal("UA", stored.IssuedFromUserAgent);
    }

    [Fact]
    public async Task IssueAsync_Caps_Expiry_At_The_Absolute_Lifetime()
    {
        // Sliding window longer than the absolute cap: expiry must follow the shorter of the two.
        await CreateSut(slidingDays: 30, absoluteDays: 7).IssueAsync(UserId, null, null);

        var stored = Assert.Single(_repo.Tokens);
        Assert.True(stored.ExpiresAt <= stored.FamilyIssuedAt.AddDays(7).AddSeconds(1));
    }

    // ---- Rotate ------------------------------------------------------------------------------

    [Fact]
    public async Task ValidateAndRotateAsync_Returns_Null_For_An_Unknown_Token()
    {
        var result = await CreateSut().ValidateAndRotateAsync("never-issued");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Revokes_The_Old_Token_And_Issues_A_New_One()
    {
        var sut = CreateSut();
        var original = await sut.IssueAsync(UserId, null, null);

        var rotated = await sut.ValidateAndRotateAsync(original);

        Assert.NotNull(rotated);
        Assert.Equal(UserId, rotated!.Value.UserId);
        Assert.NotEqual(original, rotated.Value.NewToken);
        Assert.Equal(2, _repo.Tokens.Count);

        var oldRow = _repo.Tokens[0];
        var newRow = _repo.Tokens[1];
        Assert.NotNull(oldRow.RevokedAt);
        Assert.Null(newRow.RevokedAt);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Keeps_The_New_Token_In_The_Same_Family()
    {
        var sut = CreateSut();
        var original = await sut.IssueAsync(UserId, null, null);

        await sut.ValidateAndRotateAsync(original);

        // Family identity is what makes reuse-detection able to kill the whole chain.
        Assert.Equal(_repo.Tokens[0].FamilyId, _repo.Tokens[1].FamilyId);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Links_The_Old_Row_To_Its_Replacement()
    {
        var sut = CreateSut();
        var original = await sut.IssueAsync(UserId, null, null);

        await sut.ValidateAndRotateAsync(original);

        // Regression guard: the previous implementation re-queried the new row by hash BEFORE
        // SaveChangesAsync had run, so ReplacedById was left null and the rotation chain could not
        // be walked during an incident investigation.
        Assert.Equal(_repo.Tokens[1].Id, _repo.Tokens[0].ReplacedById);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Carries_The_Family_Start_Time_Forward_Unchanged()
    {
        var sut = CreateSut();
        var original = await sut.IssueAsync(UserId, null, null);
        var familyStart = _repo.Tokens[0].FamilyIssuedAt;

        await sut.ValidateAndRotateAsync(original);

        // If rotation reset this, the absolute cap would restart on every refresh and could never
        // be reached -- which is exactly the bug the cap exists to prevent.
        Assert.Equal(familyStart, _repo.Tokens[1].FamilyIssuedAt);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Copies_The_Issuance_Context_Forward()
    {
        var sut = CreateSut();
        var original = await sut.IssueAsync(UserId, "203.0.113.7", "Mozilla/5.0");

        await sut.ValidateAndRotateAsync(original);

        Assert.Equal("203.0.113.7", _repo.Tokens[1].IssuedFromIp);
        Assert.Equal("Mozilla/5.0", _repo.Tokens[1].IssuedFromUserAgent);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Supports_Repeated_Rotation()
    {
        var sut = CreateSut();
        var token = await sut.IssueAsync(UserId, null, null);

        for (var i = 0; i < 5; i++)
        {
            var rotated = await sut.ValidateAndRotateAsync(token);
            Assert.NotNull(rotated);
            token = rotated!.Value.NewToken;
        }

        Assert.Equal(6, _repo.Tokens.Count);
        Assert.Single(_repo.Tokens.Where(t => t.RevokedAt is null));
        Assert.Single(_repo.Tokens.Select(t => t.FamilyId).Distinct());
    }

    // ---- Reuse detection ----------------------------------------------------------------------

    [Fact]
    public async Task ValidateAndRotateAsync_Kills_The_Whole_Family_When_A_Rotated_Token_Is_Replayed()
    {
        var sut = CreateSut();
        var stolen = await sut.IssueAsync(UserId, null, null);

        // Legitimate client rotates twice.
        var first = await sut.ValidateAndRotateAsync(stolen);
        var second = await sut.ValidateAndRotateAsync(first!.Value.NewToken);
        Assert.NotNull(second);

        // Attacker replays the original, already-rotated token.
        var replay = await sut.ValidateAndRotateAsync(stolen);

        Assert.Null(replay);

        // Every token in the chain -- including the legitimate client's current one -- is revoked.
        // A replay means the chain is compromised and cannot be trusted piecemeal.
        Assert.All(_repo.Tokens, t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Rejects_An_Explicitly_Revoked_Token()
    {
        var sut = CreateSut();
        var token = await sut.IssueAsync(UserId, null, null);
        await sut.RevokeAsync(token);

        var result = await sut.ValidateAndRotateAsync(token);

        Assert.Null(result);
    }

    // ---- Expiry ------------------------------------------------------------------------------

    [Fact]
    public async Task ValidateAndRotateAsync_Rejects_A_Naturally_Expired_Token()
    {
        var sut = CreateSut();
        var token = await sut.IssueAsync(UserId, null, null);

        // Wind the stored row's sliding expiry into the past.
        _repo.Tokens[0].ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);

        var result = await sut.ValidateAndRotateAsync(token);

        Assert.Null(result);

        // Natural expiry is not a theft signal, so the family is left alone rather than revoked.
        Assert.Single(_repo.Tokens);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Ends_The_Session_Once_The_Absolute_Cap_Is_Passed()
    {
        var sut = CreateSut(slidingDays: 14, absoluteDays: 90);
        var token = await sut.IssueAsync(UserId, null, null);

        // A client that kept refreshing: the token itself is fresh, but the login is 91 days old.
        _repo.Tokens[0].FamilyIssuedAt = DateTimeOffset.UtcNow.AddDays(-91);
        _repo.Tokens[0].ExpiresAt = DateTimeOffset.UtcNow.AddDays(13);

        var result = await sut.ValidateAndRotateAsync(token);

        // Regression guard: RefreshTokenAbsoluteLifetimeDays existed in JwtOptions but nothing read
        // it, so sliding expiry alone let a session live forever.
        Assert.Null(result);
        Assert.All(_repo.Tokens, t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Allows_A_Session_Still_Inside_The_Absolute_Cap()
    {
        var sut = CreateSut(slidingDays: 14, absoluteDays: 90);
        var token = await sut.IssueAsync(UserId, null, null);

        _repo.Tokens[0].FamilyIssuedAt = DateTimeOffset.UtcNow.AddDays(-89);

        var result = await sut.ValidateAndRotateAsync(token);

        Assert.NotNull(result);

        // And the replacement cannot outlive the cap: 1 day left, not a fresh 14.
        Assert.True(_repo.Tokens[1].ExpiresAt < DateTimeOffset.UtcNow.AddDays(2));
    }

    // ---- Revoke ------------------------------------------------------------------------------

    [Fact]
    public async Task RevokeAsync_Ends_The_Entire_Session_Not_Just_One_Token()
    {
        var sut = CreateSut();
        var first = await sut.IssueAsync(UserId, null, null);
        var rotated = await sut.ValidateAndRotateAsync(first);

        await sut.RevokeAsync(rotated!.Value.NewToken);

        // Regression guard: revoking only the presented row left every sibling in the family active,
        // so a token captured earlier in the chain still worked after an explicit logout.
        Assert.All(_repo.Tokens, t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task RevokeAsync_Is_Silent_For_An_Unknown_Token()
    {
        await CreateSut().RevokeAsync("never-issued");

        // Logout must not leak whether a token was real.
        Assert.Empty(_repo.Tokens);
    }

    [Fact]
    public async Task RevokeAsync_Is_Idempotent()
    {
        var sut = CreateSut();
        var token = await sut.IssueAsync(UserId, null, null);

        await sut.RevokeAsync(token);
        var firstRevokedAt = _repo.Tokens[0].RevokedAt;

        await sut.RevokeAsync(token);

        // A second logout must not overwrite when the session actually ended.
        Assert.Equal(firstRevokedAt, _repo.Tokens[0].RevokedAt);
    }

    /// <summary>
    /// Minimal in-memory stand-in for IRefreshTokenRepository. Tracks rows by reference the way EF
    /// does, so a mutation the service makes to a returned entity is visible here.
    /// </summary>
    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Tokens { get; } = [];

        public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
            => Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

        public Task AddAsync(RefreshToken token, CancellationToken ct = default)
        {
            Tokens.Add(token);
            return Task.CompletedTask;
        }

        public Task RevokeFamilyAsync(Guid familyId, CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var token in Tokens.Where(t => t.FamilyId == familyId && t.RevokedAt is null))
                token.RevokedAt = now;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
