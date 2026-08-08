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
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }

    /// <summary>Best-effort context captured at issuance for anomaly review (not used for
    /// hard binding/rejection -- a mismatch alone doesn't invalidate the token, since IP/UA
    /// legitimately change for mobile users; it's an audit signal, not an access control).</summary>
    public string? IssuedFromIp { get; set; }
    public string? IssuedFromUserAgent { get; set; }

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}
