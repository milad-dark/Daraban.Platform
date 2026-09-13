using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Daraban.IntegrationTests.Tests;

/// <summary>
/// Auth flow end-to-end (Task 8.1): register → login → access a protected endpoint.
/// Exercises the real JWT pipeline (JwtSigningKeyProvider → JwtTokenService →
/// JwtBearer validation → PermissionAuthorizationHandler) against the containerized
/// Postgres, with no mocks anywhere in the chain.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class AuthFlowTests(IntegrationTestFixture fixture)
{
    private static string UniqueUsername() => $"it_auth_{Guid.NewGuid():N}";

    [Fact]
    public async Task Register_WithValidPayload_ReturnsCreated()
    {
        var client = fixture.Api.CreateClient();

        var response = await client.PostAsJsonAsync("api/v1/identity/auth/register", new
        {
            Username = UniqueUsername(),
            Email = $"{UniqueUsername()}@test.local",
            Password = "Test-Passw0rd!2024",
            DisplayName = "Integration Test User",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ReturnsConflict()
    {
        var client = fixture.Api.CreateClient();
        var username = UniqueUsername();

        var payload = new
        {
            Username = username,
            Email = $"{username}@test.local",
            Password = "Test-Passw0rd!2024",
            DisplayName = "Integration Test User",
        };

        var first = await client.PostAsJsonAsync("api/v1/identity/auth/register", payload);
        var second = await client.PostAsJsonAsync("api/v1/identity/auth/register", payload);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndUser()
    {
        var client = fixture.Api.CreateClient();
        var username = UniqueUsername();

        await client.PostAsJsonAsync("api/v1/identity/auth/register", new
        {
            Username = username,
            Email = $"{username}@test.local",
            Password = "Test-Passw0rd!2024",
            DisplayName = "Integration Test User",
        });

        var response = await client.PostAsJsonAsync("api/v1/identity/auth/login", new
        {
            UsernameOrEmail = username,
            Password = "Test-Passw0rd!2024",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("accessToken", out var accessToken));
        Assert.False(string.IsNullOrWhiteSpace(accessToken.GetString()));
        Assert.True(root.TryGetProperty("user", out var user));
        Assert.Equal(username, user.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = fixture.Api.CreateClient();
        var username = UniqueUsername();

        await client.PostAsJsonAsync("api/v1/identity/auth/register", new
        {
            Username = username,
            Email = $"{username}@test.local",
            Password = "Test-Passw0rd!2024",
            DisplayName = "Integration Test User",
        });

        var response = await client.PostAsJsonAsync("api/v1/identity/auth/login", new
        {
            UsernameOrEmail = username,
            Password = "Wrong-Passw0rd!",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = fixture.Api.CreateClient();

        var response = await client.GetAsync("api/v1/reports/definitions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithFreshUserToken_Returns403()
    {
        // A freshly registered user has DefaultEntityId == null → active_entity_id
        // claim is Guid.Empty → PermissionAuthorizationHandler rejects with 403
        // before any permission resolution happens.
        var client = fixture.Api.CreateClient();
        var username = UniqueUsername();

        await client.PostAsJsonAsync("api/v1/identity/auth/register", new
        {
            Username = username,
            Email = $"{username}@test.local",
            Password = "Test-Passw0rd!2024",
            DisplayName = "Integration Test User",
        });

        var login = await client.PostAsJsonAsync("api/v1/identity/auth/login", new
        {
            UsernameOrEmail = username,
            Password = "Test-Passw0rd!2024",
        });
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginBody.GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var response = await client.GetAsync("api/v1/reports/definitions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
