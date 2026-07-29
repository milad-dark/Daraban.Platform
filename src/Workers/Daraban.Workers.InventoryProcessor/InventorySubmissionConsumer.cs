using Daraban.Platform.Messaging;
using Microsoft.Extensions.Options;

namespace Daraban.Workers.InventoryProcessor;

// Consumes RawInventoryReceivedEvent, evaluates import rules, upserts Assets records.
// Replaces the previous MassTransit IConsumer<object> shape with the pure-RabbitMQ.Client
// base class -- same idea (one handler per message type), no MassTransit dependency.
public class InventorySubmissionConsumer : RabbitMqConsumerBackgroundService<object> // TODO: bind to the real Contracts event type once wired
{
    protected override string QueueName => "inventory.submissions.processor";
    protected override string RoutingKey => "RawInventoryReceivedEvent";

    private readonly ILogger<InventorySubmissionConsumer> _logger;

    public InventorySubmissionConsumer(
        RabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMqOptions> options,
        ILogger<InventorySubmissionConsumer> logger)
        : base(connectionProvider, options, logger)
    {
        _logger = logger;
    }

    protected override Task HandleAsync(object message, CancellationToken ct)
    {
        _logger.LogInformation("InventorySubmissionConsumer received a message -- handling not yet implemented.");
        return Task.CompletedTask;
    }
}
