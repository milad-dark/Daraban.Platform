using Daraban.Platform.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Platform.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>Registers the pure-RabbitMQ.Client publisher. Call from any host/worker that
    /// only needs to publish events (e.g. Host.AgentApi).</summary>
    public static IServiceCollection AddRabbitMqMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        return services;
    }

    /// <summary>Registers just the connection provider + options, without the publisher --
    /// for a Worker that only consumes and never publishes. Workers that both consume and
    /// re-publish (e.g. InventoryProcessor publishing AssetCreatedEvent after processing)
    /// should call AddRabbitMqMessaging instead.</summary>
    public static IServiceCollection AddRabbitMqConsumerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<RabbitMqConnectionProvider>();
        return services;
    }
}
