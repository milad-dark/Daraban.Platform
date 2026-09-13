using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Daraban.Modules.Inventory.Data;
using Daraban.Modules.Inventory.Data.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Daraban.IntegrationTests.Tests;

/// <summary>
/// Inventory ingestion end-to-end (Task 8.1): agent token → POST inventory → 202 →
/// RawInventorySubmission row persisted. Exercises the full agent pipeline:
/// AgentAuthService (client_credentials, SHA-256 FixedTimeEquals, scope intersection)
/// → AgentScopeAuthorizationHandler → InventoryService (idempotency hash, raw
/// envelope storage, event publish) against real Postgres + RabbitMQ.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class InventoryIngestionTests(IntegrationTestFixture fixture)
{
    private static string UniqueName() => $"it_agent_{Guid.NewGuid():N}";

    [Fact]
    public async Task AgentToken_WithValidCredentials_ReturnsTokenWithScope()
    {
        var (_, clientId, clientSecret) = await fixture.SeedAgentAsync(UniqueName(), "inventory:write");

        var client = fixture.AgentApi.CreateClient();
        var response = await client.PostAsJsonAsync("api/v1/agents/auth/token", new
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Scope = "inventory:write",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("accessToken").GetString()));
        Assert.Equal("Bearer", root.GetProperty("tokenType").GetString());
        Assert.Equal("inventory:write", root.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task AgentToken_WithWrongSecret_Returns403()
    {
        var (_, clientId, _) = await fixture.SeedAgentAsync(UniqueName(), "inventory:write");

        var client = fixture.AgentApi.CreateClient();
        var response = await client.PostAsJsonAsync("api/v1/agents/auth/token", new
        {
            ClientId = clientId,
            ClientSecret = "totally-wrong-secret",
            Scope = "inventory:write",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AgentToken_RequestingExcessiveScope_Returns403()
    {
        // Scope intersection rule: requesting a scope beyond the agent's AllowedScopes
        // is denied outright (no partial grant).
        var (_, clientId, clientSecret) = await fixture.SeedAgentAsync(UniqueName(), "inventory:write");

        var client = fixture.AgentApi.CreateClient();
        var response = await client.PostAsJsonAsync("api/v1/agents/auth/token", new
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Scope = "inventory:write assets:read",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SubmitInventory_WithValidAgentToken_Returns202AndPersistsRow()
    {
        var (agentId, clientId, clientSecret) = await fixture.SeedAgentAsync(UniqueName(), "inventory:write");

        // 1. Get agent token
        var authClient = fixture.AgentApi.CreateClient();
        var tokenResponse = await authClient.PostAsJsonAsync("api/v1/agents/auth/token", new
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Scope = "inventory:write",
        });
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = tokenBody.GetProperty("accessToken").GetString();
        Assert.NotNull(token);

        // 2. Submit inventory envelope
        var inventoryClient = fixture.AgentApi.CreateClient();
        inventoryClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var deviceId = $"device-{Guid.NewGuid():N}";
        var response = await inventoryClient.PostAsJsonAsync("api/agent/inventory", new
        {
            DeviceId = deviceId,
            ItemType = "asset",
            Action = "upsert",
            TimestampUtc = DateTimeOffset.UtcNow,
            Content = new { hostname = "test-host", cpu_cores = 8 },
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var submissionId = body.GetProperty("submissionId").GetInt64();
        Assert.True(submissionId > 0);
        Assert.Equal("Accepted", body.GetProperty("status").GetString());

        // 3. Verify the raw submission row landed in Postgres
        using var scope = fixture.CreateApiScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var submission = await db.RawInventorySubmissions.FindAsync(submissionId);

        Assert.NotNull(submission);
        Assert.Equal(agentId, submission.AgentId);
        Assert.Equal(deviceId, submission.DeviceId);
        Assert.Equal(SubmissionStatus.Pending, submission.Status);
        Assert.Contains("test-host", submission.RawPayload);
    }

    [Fact]
    public async Task SubmitInventory_WithoutToken_Returns401()
    {
        var client = fixture.AgentApi.CreateClient();

        var response = await client.PostAsJsonAsync("api/agent/inventory", new
        {
            DeviceId = "device-x",
            ItemType = "asset",
            Action = "upsert",
            TimestampUtc = DateTimeOffset.UtcNow,
            Content = new { },
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SubmitInventory_SameDeviceSameMinute_ReturnsDuplicate()
    {
        // Idempotency: the hash is (agentId, deviceId, timestamp truncated to the
        // minute) — a replayed envelope must return the original submission, not a new row.
        var (agentId, clientId, clientSecret) = await fixture.SeedAgentAsync(UniqueName(), "inventory:write");

        var authClient = fixture.AgentApi.CreateClient();
        var tokenResponse = await authClient.PostAsJsonAsync("api/v1/agents/auth/token", new
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            Scope = "inventory:write",
        });
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = tokenBody.GetProperty("accessToken").GetString();

        var inventoryClient = fixture.AgentApi.CreateClient();
        inventoryClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var timestamp = DateTimeOffset.UtcNow;
        var deviceId = $"device-{Guid.NewGuid():N}";
        var envelope = new
        {
            DeviceId = deviceId,
            ItemType = "asset",
            Action = "upsert",
            TimestampUtc = timestamp,
            Content = new { hostname = "dup-host" },
        };

        var first = await inventoryClient.PostAsJsonAsync("api/agent/inventory", envelope);
        var second = await inventoryClient.PostAsJsonAsync("api/agent/inventory", envelope);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);

        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Accepted", firstBody.GetProperty("status").GetString());
        Assert.Equal("Duplicate", secondBody.GetProperty("status").GetString());
        Assert.Equal(
            firstBody.GetProperty("submissionId").GetInt64(),
            secondBody.GetProperty("submissionId").GetInt64());
    }
}
