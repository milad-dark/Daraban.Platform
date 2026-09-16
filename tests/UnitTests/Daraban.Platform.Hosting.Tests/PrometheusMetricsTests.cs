using System.Net;
using Daraban.Platform.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Daraban.Platform.Hosting.Tests;

/// <summary>
/// Boots a REAL Kestrel server on loopback with nothing but the Task 8.4 metrics
/// wiring (UseDarabanHttpMetrics + MapDarabanMetrics) plus one dummy endpoint.
/// No database, no containers, no modules -- so this runs anywhere, including CI
/// stages without Docker. Purpose: pin the exact Prometheus series and label
/// names the dashboards (deploy/monitoring), the alert rules (rules.yml) and the
/// integration test (MetricsEndpointTests) all depend on. If a prometheus-net
/// upgrade renames a series, THIS test fails first, pointing at the exact name.
/// </summary>
public sealed class PrometheusMetricsTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient? _client;
    private string? _baseAddress;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // Suppress the shared-serilog/hosting startup noise: this fixture tests
            // the metrics middleware, not logging configuration.
            EnvironmentName = Environments.Production,
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        app.UseDarabanHttpMetrics();
        app.MapDarabanMetrics();
        app.MapGet("/probe/ok", () => Results.Ok(new { ok = true }));
        app.MapGet("/probe/fail", () => Results.Problem("boom", statusCode: 500));

        await app.StartAsync();
        _app = app;
        _baseAddress = app.Urls.First().TrimEnd('/');
        _client = new HttpClient { BaseAddress = new Uri(_baseAddress) };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
            await _app.StopAsync();
    }

    [Fact]
    public async Task MetricsEndpoint_Answers_200_With_Prometheus_Content_Type()
    {
        var response = await _client!.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/plain",
            response.Content.Headers.ContentType?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task Observed_Requests_Appear_As_Counter_With_Method_And_Code_Labels()
    {
        // Generate traffic first: counters only exist after observation.
        Assert.Equal(HttpStatusCode.OK, (await _client!.GetAsync("/probe/ok")).StatusCode);
        var fail = await _client.GetAsync("/probe/fail");
        Assert.Equal(HttpStatusCode.InternalServerError, fail.StatusCode);

        var body = await _client.GetStringAsync("/metrics");

        // Exact series names, asserted character-for-character against a live scrape
        // (prometheus-net.AspNetCore 8.x uses the http_requests_* prefix -- there is no
        // _server_ infix; dashboards, alert rules and the integration test hard-code
        // these same strings).
        Assert.Contains("http_requests_received_total", body);
        Assert.Contains("http_request_duration_seconds", body);
        Assert.Contains("http_requests_in_progress", body);

        // Label names on the counter. A 200 and a 500 must be distinguishable --
        // otherwise the DarabanErrorSpike alert (code=~"5..") cannot exist.
        Assert.Contains("http_requests_received_total{", body);
        var counterLines = body.Split('\n')
            .Where(l => l.StartsWith("http_requests_received_total{"));
        var labelText = string.Join('\n', counterLines);
        Assert.Contains("method=\"GET\"", labelText);
        Assert.Contains("code=\"200\"", labelText);
        Assert.Contains("code=\"500\"", labelText);
    }

    [Fact]
    public async Task Process_Gauges_Exist_Without_Any_Traffic()
    {
        // A fresh scrape (no /probe/* calls in THIS test -- xUnit isolates fixture
        // instances per test class, but the registry is process-wide; gauges are
        // unconditional so the assertion holds regardless).
        var body = await _client!.GetStringAsync("/metrics");

        Assert.Contains("process_working_set_bytes", body);
    }
}
