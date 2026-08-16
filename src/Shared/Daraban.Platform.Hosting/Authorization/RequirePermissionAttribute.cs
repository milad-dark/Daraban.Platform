using Microsoft.AspNetCore.Authorization;

namespace Daraban.Platform.Hosting.Authorization;

/// <summary>
/// [RequirePermission("assets.write")] on a controller or action. Sets Policy to the
/// permission string with a fixed prefix so DynamicPermissionPolicyProvider can recognize
/// and synthesize a policy for it on the fly -- there is no pre-registered named policy per
/// permission string, which is the whole point given "module.action" is effectively an open
/// set (Task 2.4: "Dynamic Permission Provider").
/// </summary>
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "permission:";

    public RequirePermissionAttribute(string permission) : base(PolicyPrefix + permission) { }
}
