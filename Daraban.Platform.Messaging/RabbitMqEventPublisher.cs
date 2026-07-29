using Daraban.Platform.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text.Json;

namespace Daraban.Platform.Messaging;

/// <summary>
/// Pure RabbitMQ.Client implementation of IEventPublisher (Task 1.1: cross-module
/// decoupling via a plain pub/sub interface, deliberately not a MediatR notification).
/// Publishes to one topic exchange; routing key is the event's own type name, so a
/// consumer binds a queue to "AssetCreatedEvent", "RawInventoryReceivedEvent", etc.
/// A channel is opened per publish call rather than sharing one across threads -- IChannel
/// is not thread-safe in RabbitMQ.Client, and per-call channels are the simplest way to
/// avoid that without a channel-pooling layer this project doesn't need yet.
/// </summary>
public sealed class RabbitMqEventPublisher : IEventPublisher
{
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly RabbitMqOptions _options;

    public RabbitMqEventPublisher(RabbitMqConnectionProvider connectionProvider, IOptions<RabbitMqOptions> options)
    {
        _connectionProvider = connectionProvider;
        _options = options.Value;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class
    {
        var connection = await _connectionProvider.GetConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: ct);

        var routingKey = typeof(TEvent).Name;
        var body = JsonSerializer.SerializeToUtf8Bytes(@event);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Type = routingKey,
        };

        await channel.BasicPublishAsync(_options.ExchangeName, routingKey, mandatory: false, properties, body, ct);
    }
}
