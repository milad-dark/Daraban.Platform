using Daraban.Platform.Abstractions;

namespace Daraban.Modules.Identity.Services.Authorization;

/// <summary>
/// A resolver for callers that were constructed without one -- unit tests, and any future
/// one-off tool that mutates users outside a host's DI graph.
///
/// <see cref="ResolveAsync"/> returns the empty set, never "everything": a no-op resolver that
/// resolved permissively would turn "we forgot to wire the cache invalidation" into a privilege
/// escalation, which is exactly the failure mode this whole class exists to avoid. Invalidation is
/// genuinely a no-op, since there is no cache.
/// </summary>
internal sealed class NullPermissionResolver : IPermissionResolver
{
    public static readonly NullPermissionResolver Instance = new();

    private NullPermissionResolver() { }

    public Task<IReadOnlySet<string>> ResolveAsync(Guid userId, Guid entityId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    public Task InvalidateAsync(Guid userId, Guid entityId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task InvalidateUserAsync(Guid userId, CancellationToken ct = default)
        => Task.CompletedTask;
}