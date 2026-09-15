namespace Daraban.Platform.Abstractions;

/// <summary>Plain pub/sub used for cross-module decoupling (Task 1.1 SS1) -- deliberately not a
/// MediatR INotification. In-process today, RabbitMQ/MassTransit-backed per Task 1.1 without any
/// module code changing.</summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class;
}

public interface IPermissionResolver
{
    Task<IReadOnlySet<string>> ResolveAsync(Guid userId, Guid entityId, CancellationToken ct = default);

    /// <summary>Busts the cached result for one user+entity pair -- call this from any
    /// future code path that mutates UserProfileEntity/ProfileRight, so a rights change
    /// takes effect on the next request instead of waiting out the cache TTL (Task 1.3
    /// SS4.3).</summary>
    Task InvalidateAsync(Guid userId, Guid entityId, CancellationToken ct = default);

    /// <summary>
        /// Busts every cached result belonging to one user across all entities they hold grants in,
        /// including the descendants of any recursive grant. This is what account-state changes call:
        /// <c>UserService.SetActiveAsync</c> and <c>DeleteAsync</c> have no single entity in hand, and
        /// a disabled or deleted user's resolved permission set should not stay resident in Redis
        /// until its TTL lapses.
        ///
        /// Residual limitation: it derives the entity list from the grants that currently exist, so it
        /// cannot evict an entry whose grant has *already* been deleted. A future profile/right
        /// mutation endpoint must invalidate the affected users before removing the grant rows, or move
        /// to a version counter in the cache key. There is no such endpoint today.
    /// </summary>
    Task InvalidateUserAsync(Guid userId, CancellationToken ct = default);
}
