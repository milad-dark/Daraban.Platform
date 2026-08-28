using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Daraban.Host.AgentApi.Authorization;

/// <summary>
/// Validates that the authenticated agent's JWT contains the required scope(s).
/// Agent tokens store scopes as a space-delimited string in the "scope" claim
/// (matching the OAuth2 convention used by AgentAuthService.IssueTokenAsync).
///
/// Also supports wildcard scopes: if the agent has scope "*", all scope checks pass.
/// </summary>
public sealed class AgentScopeAuthorizationHandler : AuthorizationHandler<AgentScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, AgentScopeRequirement requirement)
    {
        // Must be an agent (is_agent claim must be "true")
        var isAgent = context.User.FindFirst("is_agent")?.Value;
        if (isAgent != "true")
        {
            context.Fail(new AuthorizationFailureReason(this, "This endpoint requires an agent token."));
            return Task.CompletedTask;
        }

        // Read the space-delimited scope claim
        var scopeClaim = context.User.FindFirst("scope")?.Value;
        if (string.IsNullOrWhiteSpace(scopeClaim))
        {
            context.Fail(new AuthorizationFailureReason(this, "Agent token has no scope claim."));
            return Task.CompletedTask;
        }

        var agentScopes = scopeClaim
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Wildcard: agent has full access
        if (agentScopes.Contains("*", StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check if the required scope is present (exact match or prefix match)
        // e.g. "inventory:write" satisfies "inventory:write" but also "inventory:*"
        if (agentScopes.Any(s => ScopeMatches(s, requirement.Scope)))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this,
                $"Agent token lacks required scope '{requirement.Scope}'. Granted: {scopeClaim}"));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks whether a granted scope satisfies a required scope.
    /// Supports exact match and wildcard prefix (e.g. "inventory:*" satisfies "inventory:write").
    /// </summary>
    private static bool ScopeMatches(string granted, string required)
    {
        if (string.Equals(granted, required, StringComparison.OrdinalIgnoreCase))
            return true;

        // Wildcard prefix: "inventory:*" matches "inventory:write", "inventory:read", etc.
        if (granted.EndsWith(":*", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = granted[..^2]; // remove ":*"
            return required.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
