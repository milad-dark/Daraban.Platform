using Daraban.Platform.Messaging;
using Microsoft.Extensions.Options;

namespace Daraban.Workers.NotificationDispatcher;

// Consumes notification-trigger events, resolves templates, sends via configured channels.
// Replaces the previous MassTransit IConsumer<object> shape with the pure-RabbitMQ.Client
// base class -- same idea (one handler per message type), no MassTransit dependency.
public class QueuedNotificationConsumer : RabbitMqConsumerBackgroundService<object> // TODO: bind to the real Contracts event type once wired
{
    protected override string QueueName => "notifications.dispatcher";
    protected override string RoutingKey => "NotificationTriggeredEvent";

    private readonly ILogger<QueuedNotificationConsumer> _logger;

    public QueuedNotificationConsumer(
        RabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMqOptions> options,
        ILogger<QueuedNotificationConsumer> logger)
        : base(connectionProvider, options, logger)
    {
        _logger = logger;
    }

    protected override Task HandleAsync(object message, CancellationToken ct)
    {
        _logger.LogInformation("QueuedNotificationConsumer received a message -- handling not yet implemented.");
        return Task.CompletedTask;
    }
}
