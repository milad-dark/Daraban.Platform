using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Daraban.Platform.Hosting.Authorization;

/// <summary>
/// Task 2.4's "Dynamic Permission Provider": [RequirePermission("assets.write")] sets
/// Policy = "permission:assets.write". Since permission strings are effectively an open
/// set (every module.action combination, growing as modules are built out), pre-registering
/// a named AuthorizationPolicy per permission via AddAuthorizationBuilder().AddPolicy(...)
/// doesn't scale -- this provider intercepts any policy name with the "permission:" prefix
/// and synthesizes a policy with a PermissionRequirement on the fly instead. Anything else
/// (ordinary named policies, if any exist) falls through to the default provider.
/// </summary>
public sealed class DynamicPermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public DynamicPermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var permission = policyName[RequirePermissionAttribute.PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
