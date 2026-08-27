using Daraban.Platform.Common;

namespace Daraban.Modules.Identity.Data.Entities;

/// <summary>
/// OAuth2 client credential (client_id + hashed client_secret) for an Agent.
/// Supports multiple active credentials per agent for rotation without downtime.
/// The client_id is public; the client_secret is hashed with SHA-256 before storage
/// and never returned after creation (Task 4.1 SS1.2).
/// </summary>
public class AgentCredential : BaseEntity
{
    public Guid AgentId { get; set; }

    /// <summary>Public client identifier. Used as the OAuth2 client_id.</summary>
    public string ClientId { get; set; } = default!;

    /// <summary>SHA-256 hash of the client_secret. The plain secret is only returned once at creation time.</summary>
    public string ClientSecretHash { get; set; } = default!;

    /// <summary>Human-readable label (e.g. "Production key", "CI/CD pipeline").</summary>
    public string? Label { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>When this credential was last used to obtain a token.</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>Optional expiry. NULL = no expiry.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Scopes this specific credential is limited to (subset of Agent.AllowedScopes).</summary>
    public string? Scopes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // ---- Navigation ----
    public Agent Agent { get; set; } = null!;
}
