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

    public async Task<string> IssueAsync(Guid userId, string? issuedFromIp, string? issuedFromUserAgent, CancellationToken ct = default)
        => await IssueInternalAsync(userId, Guid.NewGuid(), issuedFromIp, issuedFromUserAgent, ct);

    public async Task<(Guid UserId, string NewToken)?> ValidateAndRotateAsync(string presentedToken, CancellationToken ct = default)
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

        if (!existing.IsActive) return null; // naturally expired

        existing.RevokedAt = DateTimeOffset.UtcNow;
        var newToken = await IssueInternalAsync(existing.UserId, existing.FamilyId, existing.IssuedFromIp, existing.IssuedFromUserAgent, ct);
        existing.ReplacedById = null; // set below once the new row's Id is known
        var newHash = Hash(newToken);
        var newRow = await _repository.GetByTokenHashAsync(newHash, ct);
        existing.ReplacedById = newRow?.Id;

        await _repository.SaveChangesAsync(ct);
        return (existing.UserId, newToken);
    }

    public async Task RevokeAsync(string presentedToken, CancellationToken ct = default)
    {
        var hash = Hash(presentedToken);
        var existing = await _repository.GetByTokenHashAsync(hash, ct);
        if (existing is null || existing.RevokedAt is not null) return;

        existing.RevokedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(ct);
    }

    private async Task<string> IssueInternalAsync(Guid userId, Guid familyId, string? ip, string? userAgent, CancellationToken ct)
    {
        var raw = GenerateRawToken();
        var now = DateTimeOffset.UtcNow;

        await _repository.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = Hash(raw),
            IssuedAt = now,
            ExpiresAt = now.AddDays(_options.RefreshTokenLifetimeDays),
            IssuedFromIp = ip,
            IssuedFromUserAgent = userAgent,
        }, ct);
        await _repository.SaveChangesAsync(ct);

        return raw;
    }

    private static string GenerateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); // 256 bits

    private static string Hash(string token)
        => Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
