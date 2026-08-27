using Daraban.Platform.Common;

namespace Daraban.Modules.Identity.Services.Agents;

/// <summary>
/// OAuth2 client_credentials token exchange for agents (Task 4.1 SS1.1).
/// Agents authenticate as machines — no user session, no refresh tokens.
/// The issued access token is a short-lived JWT with agent identity claims and scope claims.
/// </summary>
public interface IAgentAuthService
{
    /// <summary>
    /// Exchange client_id + client_secret for a short-lived access token (OAuth2 client_credentials).
    /// Validates: credential exists, is active, not expired, agent is active, requested scopes ⊆ allowed.
    /// On success, logs the auth event to the audit trail.
    /// </summary>
    Task<Result<TokenResponse>> GetTokenAsync(TokenRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default);
}
