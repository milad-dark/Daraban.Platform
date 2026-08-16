using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Platform.Hosting;

/// <summary>
/// Two endpoints, two different questions (Task 2.2):
///   /health/live  -- "is the process up at all?" No dependency checks -- if this fails,
///                    restarting the process is the right call (container orchestrator
///                    liveness probe).
///   /health/ready -- "can this process actually serve traffic right now?" Checks every
///                    tagged dependency (Postgres always; Redis/RabbitMQ only for the
///                    hosts/workers that actually use them) -- if this fails, take the
///                    instance out of the load balancer rotation, don't restart it (a
///                    down database isn't fixed by restarting the API).
/// Uses the free, MIT-licensed Xabaril AspNetCore.Diagnostics.HealthChecks packages for the
/// actual Postgres/Redis/RabbitMQ probes -- ASP.NET Core's own AddHealthChecks() only gives
/// you the framework, not dependency-specific checks.
/// </summary>
public static class HealthCheckExtensions
{
    public const string ReadyTag = "ready";

    public static IHealthChecksBuilder AddDarabanHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddHealthChecks();

        var postgres = configuration.GetConnectionString("Postgres");
        if (!string.IsNullOrWhiteSpace(postgres))
            builder.AddNpgSql(postgres, name: "postgres", tags: [ReadyTag]);

        var redis = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redis))
            builder.AddRedis(redis, name: "redis", tags: [ReadyTag]);

        var rabbitHost = configuration["RabbitMq:Host"];
        if (!string.IsNullOrWhiteSpace(rabbitHost))
        {
            var user = Uri.EscapeDataString(configuration["RabbitMq:Username"] ?? "guest");
            var pass = Uri.EscapeDataString(configuration["RabbitMq:Password"] ?? "guest");
            var port = configuration["RabbitMq:Port"] ?? "5672";
            var uri = $"amqp://{user}:{pass}@{rabbitHost}:{port}";
            builder.AddRabbitMQ(name: "rabbitmq", tags: [ReadyTag]);
        }

        return builder;
    }

    public static WebApplication MapDarabanHealthCheckEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            // No predicate => no registered checks run at all here; this endpoint only
            // answers "did the process start and can it respond to HTTP." Deliberately
            // cheap and dependency-free.
            Predicate = _ => false,
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse, // structured JSON incl. per-check status/duration, not just a bare string
        }).AllowAnonymous();

        return app;
    }
}
