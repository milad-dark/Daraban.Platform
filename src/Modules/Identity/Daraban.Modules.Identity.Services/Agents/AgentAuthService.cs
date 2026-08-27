using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Modules.Identity.Services.Auth;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Daraban.Modules.Identity.Services.Agents;

/// <summary>
/// OAuth2 client_credentials flow for agents (Task 4.1 SS1.1).
/// Agents authenticate as machines — no user session, no refresh tokens.
/// The issued JWT contains:
///   sub: agent ID
///   agent_type: the agent's AgentType
///   scope: granted scopes (intersection of allowed ∩ requested)
///   active_entity_id: the agent's entity scope
/// No "token_version" claim — agent tokens are revoked by deactivating the credential, not the agent.
/// </summary>
public class AgentAuthService(
    IAgentRepository repo,
    IAgentService agentService,
    IOptions<JwtOptions> jwtOptions,
    JwtSigningKeyProvider keyProvider) : IAgentAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<Result<TokenResponse>> GetTokenAsync(
        TokenRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        // 1. Look up credential by client_id
        var credential = await repo.GetCredentialByClientIdAsync(request.ClientId, ct);
        if (credential is null)
        {
            // Timing-safe: hash a dummy to prevent client_id enumeration via timing
            AgentService.HashSecret("dummy-to-equalize-timing");
            return Result.Failure<TokenResponse>(new Error("AGENTS.AUTH_INVALID_CREDENTIALS", "Invalid client credentials.", ErrorType.Forbidden));
        }

        // 2. Check credential is active
        if (!credential.IsActive)
            return Result.Failure<TokenResponse>(new Error("AGENTS.AUTH_CREDENTIAL_REVOKED", "This credential has been revoked.", ErrorType.Forbidden));

        // 3. Check credential expiry
        if (credential.ExpiresAt.HasValue && credential.ExpiresAt.Value < DateTimeOffset.UtcNow)
            return Result.Failure<TokenResponse>(new Error("AGENTS.AUTH_CREDENTIAL_EXPIRED", "This credential has expired.", ErrorType.Forbidden));

        // 4. Verify client_secret (SHA-256 comparison)
        var providedHash = AgentService.HashSecret(request.ClientSecret);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(providedHash),
                Encoding.UTF8.GetBytes(credential.ClientSecretHash)))
        {
            return Result.Failure<TokenResponse>(new Error("AGENTS.AUTH_INVALID_CREDENTIALS", "Invalid client credentials.", ErrorType.Forbidden));
        }

        // 5. Check agent is active (credential.Agent may be null if the agent was
        //    soft-deleted — the query filter on Agent excludes it from Include).
        var agent = credential.Agent;
        if (agent is null || agent.Status != Data.Entities.AgentStatus.Active)
            return Result.Failure<TokenResponse>(new Error("AGENTS.AUTH_AGENT_INACTIVE", "Agent is not active.", ErrorType.Forbidden));

        // 6. Validate requested scopes ⊆ (credential scopes ∩ agent allowed scopes)
        var grantedScopes = ResolveGrantedScopes(
            request.Scope,
            credential.Scopes ?? agent.AllowedScopes,
            agent.AllowedScopes);

        if (grantedScopes is null)
            return Result.Failure<TokenResponse>(new Error("AGENTS.AUTH_SCOPE_DENIED", "Requested scopes exceed allowed permissions.", ErrorType.Forbidden));

        // 7. Issue JWT
        var now = DateTimeOffset.UtcNow;
        var expiresIn = (int)_jwtOptions.AccessTokenLifetimeMinutes * 60;
        var expiresAt = now.AddSeconds(expiresIn);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, agent.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("agent_type", agent.Type.ToString()),
            new Claim("scope", grantedScopes),
            new Claim("is_agent", "true"),
            new Claim("name", agent.Name),
            // Entity scope — NULL means no entity restriction
            new Claim("active_entity_id", agent.EntityId?.ToString() ?? string.Empty),
        };

        var credentials = new SigningCredentials(
            new RsaSecurityKey(keyProvider.GetKey()), SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        // 8. Update last-used timestamp (fire-and-forget, don't fail auth on this)
        credential.LastUsedAt = now;
        credential.UpdatedAt = now;
        repo.UpdateCredential(credential);

        // Touch agent last active
        agent.LastActiveAt = now;
        agent.UpdatedAt = now;
        repo.Update(agent);

        await repo.SaveChangesAsync(ct);

        // 9. Audit the successful auth — fire-and-forget: a failed audit write must not
        // prevent token issuance, so we catch and swallow any exception.
        try
        {
            await agentService.LogActionAsync(
                agent.Id, credential.Id, "auth.token_issued",
                $"scope={grantedScopes}", null, ipAddress, userAgent,
                null, true, null, null, agent.EntityId, null, ct);
        }
        catch
        {
            // Audit failure is non-fatal — token has already been generated.
            // TODO: log the failure via ILogger when one is injected.
        }

        return Result.Success(new TokenResponse(tokenString, "Bearer", expiresIn, grantedScopes));
    }

    /// <summary>
    /// Computes the intersection of: requested scopes, credential-limited scopes, and agent-allowed scopes.
    /// Returns null if no valid scopes remain (denied).
    /// Returns "*" if wildcard is in the intersection.
    /// </summary>
    private static string? ResolveGrantedScopes(string? requestedScope, string credentialScopes, string agentAllowedScopes)
    {
        var allowed = agentAllowedScopes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var credLimited = credentialScopes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Credential scopes ⊆ Agent allowed scopes (pre-condition); intersect them
        var available = allowed.Intersect(credLimited, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // If wildcard, all agent-allowed scopes are available
        if (available.Contains("*"))
            available = allowed;

        if (string.IsNullOrWhiteSpace(requestedScope))
        {
            // No specific scopes requested — grant all available
            return string.Join(" ", available);
        }

        var requested = requestedScope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var granted = requested.Where(s => available.Contains(s, StringComparer.OrdinalIgnoreCase)).ToList();

        if (granted.Count == 0)
            return null; // nothing overlaps → denied

        // Check if all requested scopes are granted
        if (granted.Count != requested.Length)
            return null; // partial match → denied (don't grant partial)

        return string.Join(" ", granted);
    }
}
