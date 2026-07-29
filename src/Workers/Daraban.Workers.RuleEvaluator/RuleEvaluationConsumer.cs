using Daraban.Platform.Messaging;
using Microsoft.Extensions.Options;

namespace Daraban.Workers.RuleEvaluator;

// Runs Automation's rule evaluation for non-inventory triggers (e.g. ticket auto-assignment).
// Replaces the previous MassTransit IConsumer<object> shape with the pure-RabbitMQ.Client
// base class -- same idea (one handler per message type), no MassTransit dependency.
public class RuleEvaluationConsumer : RabbitMqConsumerBackgroundService<object> // TODO: bind to the real Contracts event type once wired
{
    protected override string QueueName => "automation.rule-evaluator";
    protected override string RoutingKey => "RuleEvaluationRequestedEvent";

    private readonly ILogger<RuleEvaluationConsumer> _logger;

    public RuleEvaluationConsumer(
        RabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMqOptions> options,
        ILogger<RuleEvaluationConsumer> logger)
        : base(connectionProvider, options, logger)
    {
        _logger = logger;
    }

    protected override Task HandleAsync(object message, CancellationToken ct)
    {
        _logger.LogInformation("RuleEvaluationConsumer received a message -- handling not yet implemented.");
        return Task.CompletedTask;
    }
}
