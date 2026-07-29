namespace Daraban.Platform.Messaging;

/// <summary>Binds to the "RabbitMq" configuration section already present in every
/// host's/worker's appsettings.json (Task 2.1) -- unchanged by dropping MassTransit.</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    /// <summary>Single topic exchange every event is published to; routing key is the
    /// event's own name (see RabbitMqEventPublisher). One exchange keeps the "pure
    /// RabbitMQ.Client" setup simple -- split into per-module exchanges later only if a
    /// real need for that isolation shows up.</summary>
    public string ExchangeName { get; set; } = "daraban.events";
}
