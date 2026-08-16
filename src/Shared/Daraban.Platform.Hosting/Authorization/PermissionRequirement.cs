using Microsoft.AspNetCore.Authorization;

namespace Daraban.Platform.Hosting.Authorization;

/// <summary>One requirement per permission string (Task 1.3 SS4.2 convention:
/// "module.action", e.g. "assets.write", "servicedesk.tickets.read.own").</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) => Permission = permission;
}
