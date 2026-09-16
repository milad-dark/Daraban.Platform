using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;

namespace Daraban.Platform.Hosting;

/// <summary>
/// Prometheus exposition for Task 8.4: every service exposes GET /metrics in the standard
/// text format, scraped by Prometheus over the internal docker network.
///
/// Two entry points, matching the pipeline half of the health-check pattern
/// (MapDarabanHealthCheckEndpoints): there is deliberately no AddDarabanMetrics
/// service-registration step -- neither the middleware nor the registry needs
/// anything from DI, so there is nothing to register.
///   UseDarabanHttpMetrics() -- request-count/duration/in-flight middleware. Registered
///     FIRST in the pipeline (right after the exception handler) so rejected requests
///     (401/429) are counted too -- an unauthenticated flood is exactly what the error/
///     rate alerts need to see.
///   MapDarabanMetrics() -- the /metrics endpoint itself.
///
/// SECURITY: /metrics is anonymous by design (Prometheus cannot present a JWT) AND it is
/// never routed by nginx -- there is intentionally no location block for it in either
/// nginx.conf or deploy/nginx/nginx.prod.conf. Scraping happens over the compose network
/// via container DNS (host-api:8080, host-agentapi:8081). Anyone who can reach the
/// container network directly is already inside the trust boundary; the public edge only
/// ever sees :80/:443 through nginx.
/// </summary>
public static class PrometheusMetricsExtensions
{
    public static IApplicationBuilder UseDarabanHttpMetrics(this IApplicationBuilder app)
    {
        app.UseHttpMetrics();
        return app;
    }

    public static WebApplication MapDarabanMetrics(this WebApplication app)
    {
        // Minimal endpoints carry no [Authorize] metadata, so this stays anonymous without an
        // explicit AllowAnonymous() -- stated here so the next reader doesn't "fix" it by
        // adding authorization and silently break every scrape with 401s.
        app.MapMetrics();
        return app;
    }

    /// <summary>
    /// Worker counterpart of MapDarabanMetrics (Task 8.4: "all services" really means all --
    /// the six background workers have no Kestrel, so there is nothing to map a route onto).
    /// Starts prometheus-net's standalone HttpListener-based MetricServer on the shared
    /// internal port. Every worker container has its own network namespace, so they can all
    /// listen on the SAME port -- Prometheus tells them apart by container DNS name
    /// (worker-automation:9102, worker-notifications:9102, ...). Nothing is published to the
    /// host; same trust-boundary reasoning as the hosts' /metrics above.
    /// </summary>
    public static IHostApplicationBuilder AddDarabanWorkerMetrics(
        this IHostApplicationBuilder builder, int port = 9102)
    {
        builder.Services.AddSingleton<IHostedService>(_ => new MetricServerHostedService(port));
        return builder;
    }

    /// <summary>Lifecycle wrapper: binds on start, unbinds on shutdown. A bare
    /// MetricServer.Start() with no Stop would hold the port across a graceful-shutdown
    /// window and fail the next bind on fast restarts.</summary>
    private sealed class MetricServerHostedService(int port) : IHostedService
    {
        private MetricServer? _server;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _server = new MetricServer(port);
            _server.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _server?.Stop();
            return Task.CompletedTask;
        }
    }
}
