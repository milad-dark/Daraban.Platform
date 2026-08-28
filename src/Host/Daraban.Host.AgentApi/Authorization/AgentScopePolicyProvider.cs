using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Daraban.Host.AgentApi.Authorization;

/// <summary>
/// Dynamically creates authorization policies for agent scope checks.
/// Policy names follow the pattern "agent:scope:{scopeName}".
///
/// When ASP.NET Core encounters [Authorize(Policy = "agent:scope:inventory:write")],
/// this provider creates an AuthorizationPolicy containing an AgentScopeRequirement
/// for "inventory:write". This avoids pre-registering a named policy for every
/// possible scope combination (the scope set is open-ended).
///
/// Falls through to the default policy provider for any policy name that doesn't
/// match the "agent:scope:" prefix, so [Authorize] and [Authorize(Policy = "...")]
/// on non-agent endpoints continue to work as before.
/// </summary>
public sealed class AgentScopePolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private const string PolicyPrefix = "agent:scope:";
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var scope = policyName[PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder();
            policy.AddRequirements(new AgentScopeRequirement(scope));
            return Task.FromResult<AuthorizationPolicy?>(policy.Build());
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallback.GetFallbackPolicyAsync();
}
