using System.Net;
using Xunit;

namespace Daraban.IntegrationTests.Tests;

/// <summary>
/// Prometheus exposition smoke test (Task 8.4): both hosts answer GET /metrics with 200
/// and the Prometheus text format, anonymously. This is the contract the scrape configs in
/// deploy/monitoring/prometheus.yml depend on -- if an endpoint starts requiring auth or
/// moves, every scrape target breaks silently (Prometheus logs it, nobody reads that log).
///
/// Uses /health/live as the sacrificial request: it exercises the HTTP-metrics middleware
/// so the http_requests_* series exist, and it is dependency-free (no DB/Redis/RabbitMQ
/// needed for the assertion to hold).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class MetricsEndpointTests(IntegrationTestFixture fixture)
{
    [Theory]
    [InlineData("api")]
    [InlineData("agentapi")]
    public async Task MetricsEndpoint_Returns_Prometheus_Text_Format_Anonymously(string host)
    {
        var client = host == "api" ? fixture.Api.CreateClient() : fixture.AgentApi.CreateClient();

        // One request through the middleware so the http_requests_* counters exist. /health/live
        // is dependency-free by design (Task 2.2), so this holds even with the containers down.
        var probe = await client.GetAsync("health/live");
        Assert.Equal(HttpStatusCode.OK, probe.StatusCode);

        var response = await client.GetAsync("metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Prometheus text exposition format, version 0.0.4 -- the scrape config does not set
        // any special Accept handling, so the default content type is what Prometheus parses.
        var contentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty;
        Assert.Contains("text/plain", contentType);

        var body = await response.Content.ReadAsStringAsync();

        // Process/runtime gauges are always present (no requests needed)...
        Assert.Contains("process_working_set_bytes", body);

        // ...while the HTTP counter exists because of the /health/live probe above.
        // Exact name pinned by Daraban.Platform.Hosting.Tests (live Kestrel scrape).
        Assert.Contains("http_requests_received_total", body);
    }

    [Fact]
    public async Task MetricsEndpoint_Does_Not_Require_Authentication()
    {
        // Fresh client, no JWT anywhere near it. If MapMetrics ever lands behind the
        // authorization middleware, this fails with 401 -- and so would every scrape.
        var client = fixture.Api.CreateClient();

        var response = await client.GetAsync("metrics");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
