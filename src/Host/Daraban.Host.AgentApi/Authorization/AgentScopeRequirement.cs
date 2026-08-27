using Microsoft.AspNetCore.Authorization;

namespace Daraban.Host.AgentApi.Authorization;

/// <summary>
/// An authorization requirement that checks whether the authenticated agent's JWT
/// contains a specific scope. Policy names follow the pattern "agent:scope:{scopeName}".
/// Example: [Authorize(Policy = "agent:scope:inventory:write")] requires the agent
/// token to include "inventory:write" in its space-delimited scope claim.
/// </summary>
public sealed class AgentScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}
