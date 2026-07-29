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
}
