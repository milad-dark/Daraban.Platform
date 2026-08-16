using Daraban.Platform.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Daraban.Platform.Hosting.Authorization;

/// <summary>
/// Task 1.3 SS7's per-request pipeline, as an actual AuthorizationHandler: resolve the
/// caller's effective permission set for their active entity (IPermissionResolver -- cached,
/// computed from Identity's UserProfileEntity/ProfileRight tables, Task 2.4) and check the
/// required permission against it. This is the coarse "can you call this action at all"
/// layer (Task 1.3 SS4.4) -- row-level own/group/all filtering still happens inside the
/// Service method itself, this handler can't express that.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionResolver _permissionResolver;

    public PermissionAuthorizationHandler(ICurrentUser currentUser, IPermissionResolver permissionResolver)
    {
        _currentUser = currentUser;
        _permissionResolver = permissionResolver;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.ActiveEntityId == Guid.Empty)
            return; // not authenticated / malformed token -- JwtBearer's own pipeline should already have rejected this, this is a belt-and-suspenders no-op, not a Succeed

        var permissions = await _permissionResolver.ResolveAsync(_currentUser.UserId, _currentUser.ActiveEntityId);

        if (permissions.Contains(requirement.Permission))
            context.Succeed(requirement);
    }
}
