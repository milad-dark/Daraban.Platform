namespace Daraban.Modules.Identity.Data.Entities;

/// <summary>
/// Opaque, hashed-at-rest refresh token (Task 1.3 SS3). Never store the raw token -- only
/// TokenHash. FamilyId groups a chain of rotated tokens: reusing an already-rotated token
/// (a replay/theft signal) revokes the whole family, not just that one row.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public Guid FamilyId { get; set; }
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>
    /// When the FIRST token in this family was issued -- i.e. when the user actually logged in.
    /// Carried forward unchanged through every rotation so the absolute session cap
    /// (JwtOptions.RefreshTokenAbsoluteLifetimeDays) can be enforced. Without it, each rotation
    /// pushed ExpiresAt forward and a client that refreshed inside the sliding window stayed
    /// authenticated forever, which is exactly what an absolute cap exists to prevent.
    /// </summary>
    public DateTimeOffset FamilyIssuedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }

    /// <summary>Best-effort context captured at issuance for anomaly review (not used for
    /// hard binding/rejection -- a mismatch alone doesn't invalidate the token, since IP/UA
    /// legitimately change for mobile users; it's an audit signal, not an access control).</summary>
    public string? IssuedFromIp { get; set; }
    public string? IssuedFromUserAgent { get; set; }

    /// <summary>Not revoked and not past its own sliding expiry.</summary>
    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;

    /// <summary>
    /// True once the whole login session has outlived the absolute cap, regardless of how
    /// recently this individual token was rotated.
    /// </summary>
    public bool HasExceededAbsoluteLifetime(int absoluteLifetimeDays, DateTimeOffset asOf)
        => asOf >= FamilyIssuedAt.AddDays(absoluteLifetimeDays);
}
