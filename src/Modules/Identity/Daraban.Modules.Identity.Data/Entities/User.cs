using Daraban.Platform.Common;

namespace Daraban.Modules.Identity.Data.Entities;

public class User : SoftDeletableEntity
{
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? PasswordHash { get; set; }
    public string DisplayName { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public Guid? DefaultEntityId { get; set; }

    /// <summary>Bumped on password change / forced logout -- compared against the JWT's
    /// token_version claim on every request (Task 1.3 SS8) to make an otherwise-stateless
    /// short-lived access token revocable immediately.</summary>
    public int TokenVersion { get; set; }

    // ---- Auth hardening (Task 2.3) ------------------------------------------------
    /// <summary>Not yet verified via a confirmation link. Registration still succeeds
    /// (usable account created immediately) -- email confirmation gates elevated actions
    /// later rather than blocking login outright, since the Notifications module (which
    /// would actually send the email) doesn't exist yet. Wire this up for real once it does.</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>Consecutive failed login attempts since the last success. Reset to 0 on a
    /// successful login. Drives account lockout -- see AuthService.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Set when FailedLoginCount crosses the lockout threshold; null once expired
    /// or cleared by a successful login. Checked before attempting password verification.</summary>
    public DateTimeOffset? LockoutEndAt { get; set; }
}
