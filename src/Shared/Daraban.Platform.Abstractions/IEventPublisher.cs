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
}
