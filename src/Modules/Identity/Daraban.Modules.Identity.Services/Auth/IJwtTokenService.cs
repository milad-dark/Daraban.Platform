using Daraban.Modules.Identity.Data.Entities;

namespace Daraban.Modules.Identity.Services.Auth;

public interface IJwtTokenService
{
    /// <summary>Issues a short-lived access token for an interactive user session
    /// (Task 1.3 SS2.1 claim shape). activeEntityId is the caller-selected "acting in"
    /// entity from the entity-switcher UX.</summary>
    (string Token, DateTimeOffset ExpiresAt) IssueAccessToken(User user, Guid activeEntityId);
}
