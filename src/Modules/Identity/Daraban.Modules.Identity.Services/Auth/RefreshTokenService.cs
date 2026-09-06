using System.Security.Cryptography;
using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Platform.Abstractions;
using Microsoft.Extensions.Options;

namespace Daraban.Modules.Identity.Services.Auth;

/// <summary>
/// Opaque refresh tokens (Task 1.3 SS3) -- NOT a JWT. A refresh token has to be
/// unilaterally revocable server-side, which a self-contained JWT can't be without a
/// denylist that defeats the point of being stateless. 256 bits of CSPRNG entropy, hashed
/// (SHA-256) before it ever touches the database -- a DB leak alone never yields a usable
/// token. Rotated on every use; reusing an already-rotated token revokes its whole family.
/// </summary>
public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly IRefreshTokenRepository _repository;
    private readonly JwtOptions _options;

    public RefreshTokenService(IRefreshTokenRepository repository, IOptions<JwtOptions> options)
    {
        _repository = repository;
        _options = options.Value;
    }

    public async Task<string> IssueAsync(
        Guid userId, string? issuedFromIp, string? issuedFromUserAgent, CancellationToken ct = default)
    {
        // A fresh login starts a new family, so the absolute-lifetime clock starts now.
        var now = DateTimeOffset.UtcNow;
        var (raw, _) = await IssueInternalAsync(
            userId, Guid.CreateVersion7(), familyIssuedAt: now, issuedFromIp, issuedFromUserAgent, ct);

        await _repository.SaveChangesAsync(ct);
        return raw;
    }

    public async Task<(Guid UserId, string NewToken)?> ValidateAndRotateAsync(
        string presentedToken, CancellationToken ct = default)
    {
        var hash = Hash(presentedToken);
        var existing = await _repository.GetByTokenHashAsync(hash, ct);
        if (existing is null) return null;

        if (existing.RevokedAt is not null)
        {
            // Reuse of an already-rotated (or already-revoked) token: treat as theft/replay
            // and kill the whole family, not just this row (Task 1.3 SS3).
            await _repository.RevokeFamilyAsync(existing.FamilyId, ct);
            await _repository.SaveChangesAsync(ct);
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        if (!existing.IsActive) return null; // naturally expired

        // Absolute session cap. Sliding expiry alone let a client that kept refreshing inside the
        // window stay authenticated indefinitely -- RefreshTokenAbsoluteLifetimeDays existed in
        // JwtOptions but nothing ever read it. Revoking the family (not just this row) means the
        // whole session ends rather than leaving sibling tokens usable.
        if (existing.HasExceededAbsoluteLifetime(_options.RefreshTokenAbsoluteLifetimeDays, now))
        {
            await _repository.RevokeFamilyAsync(existing.FamilyId, ct);
            await _repository.SaveChangesAsync(ct);
            return null;
        }

        var (newToken, newRow) = await IssueInternalAsync(
            existing.UserId,
            existing.FamilyId,
            // Carried forward, never reset -- otherwise every rotation would restart the absolute
            // clock and the cap could never be reached.
            existing.FamilyIssuedAt,
            existing.IssuedFromIp,
            existing.IssuedFromUserAgent,
            ct);

        existing.RevokedAt = now;

        // The replacement row is already tracked, so its Id is known without a round trip. The
        // previous implementation re-queried by hash before SaveChangesAsync had run, so
        // ReplacedById was almost always left null and the rotation chain was unwalkable.
        existing.ReplacedById = newRow.Id;

        await _repository.SaveChangesAsync(ct);
        return (existing.UserId, newToken);
    }

    public async Task RevokeAsync(string presentedToken, CancellationToken ct = default)
    {
        var hash = Hash(presentedToken);
        var existing = await _repository.GetByTokenHashAsync(hash, ct);
        if (existing is null || existing.RevokedAt is not null) return;

        // Logout ends the session, not just this one token. Revoking a single row left every
        // sibling in the family active, so a token captured earlier in the chain still worked
        // after the user had explicitly logged out.
        await _repository.RevokeFamilyAsync(existing.FamilyId, ct);
        await _repository.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Creates and tracks a token row. Deliberately does NOT save -- the caller decides the
    /// transaction boundary, so a rotation writes the new row and revokes the old one atomically
    /// instead of committing the new token and then failing to revoke its predecessor.
    /// </summary>
    private async Task<(string Raw, RefreshToken Row)> IssueInternalAsync(
        Guid userId,
        Guid familyId,
        DateTimeOffset familyIssuedAt,
        string? ip,
        string? userAgent,
        CancellationToken ct)
    {
        var raw = GenerateRawToken();
        var now = DateTimeOffset.UtcNow;

        var slidingExpiry = now.AddDays(_options.RefreshTokenLifetimeDays);
        var absoluteExpiry = familyIssuedAt.AddDays(_options.RefreshTokenAbsoluteLifetimeDays);

        var row = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            FamilyId = familyId,
            FamilyIssuedAt = familyIssuedAt,
            TokenHash = Hash(raw),
            IssuedAt = now,
            // Never past the absolute cap: a token issued 89 days into a 90-day session expires
            // in 1 day, not 14.
            ExpiresAt = slidingExpiry < absoluteExpiry ? slidingExpiry : absoluteExpiry,
            IssuedFromIp = ip,
            IssuedFromUserAgent = userAgent,
        };

        await _repository.AddAsync(row, ct);
        return (raw, row);
    }

    // 256 bits of CSPRNG entropy. Base64Url so the value is safe in a cookie, a header, or a URL
    // without escaping -- plain Base64 can contain '+' and '/', which get mangled in transit.
    private static string GenerateRawToken()
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string token)
        => Base64UrlEncode(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
