using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace Daraban.Platform.Messaging;

/// <summary>
/// Replaces MassTransit's AddConsumer&lt;T&gt;()/ConfigureEndpoints() for the Workers
/// (Task 1.1 SS2.4). Each Worker's consumer class extends this, giving it a queue name and
/// the routing key(s) to bind, and implements HandleAsync for the actual message handling.
/// Declares its own durable queue bound to the shared topic exchange, consumes with manual
/// ack (a handler exception leaves the message unacked -- redelivered rather than lost,
/// which is the behavior MassTransit gave you by default too).
/// </summary>
public abstract class RabbitMqConsumerBackgroundService<TMessage> : BackgroundService where TMessage : class
{
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMqOptions _options;
    private readonly ILogger _logger;

    /// <summary>Durable queue name -- one per consumer, e.g. "inventory.submissions.processor".</summary>
    protected abstract string QueueName { get; }

    /// <summary>Routing key this consumer cares about -- matches the event type name published
    /// via RabbitMqEventPublisher, e.g. nameof(RawInventoryReceivedEvent).</summary>
    protected abstract string RoutingKey { get; }

    protected RabbitMqConsumerBackgroundService(
        RabbitMqConnectionProvider connectionProvider, IOptions<RabbitMqOptions> options, ILogger logger)
    {
        _connectionProvider = connectionProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Actual message handling -- implemented by each Worker's consumer. Throwing
    /// leaves the message unacked (redelivered); returning normally acks it.</summary>
    protected abstract Task HandleAsync(TMessage message, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await _connectionProvider.GetConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(QueueName, _options.ExchangeName, RoutingKey, cancellationToken: stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<TMessage>(ea.Body.Span);
                if (message is not null)
                    await HandleAsync(message, stoppingToken);

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed handling message on queue {Queue} -- leaving unacked for redelivery.", QueueName);
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, cancellationToken: stoppingToken);

        // Keep the service alive for the consumer's lifetime; actual work happens in the
        // ReceivedAsync callback above.
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }
}
