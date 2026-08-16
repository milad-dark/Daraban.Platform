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
    /// SS4.3). No such mutation endpoints exist yet; this is ready for when they do.</summary>
    Task InvalidateAsync(Guid userId, Guid entityId, CancellationToken ct = default);
}
