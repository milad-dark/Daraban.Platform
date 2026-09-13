using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Daraban.IntegrationTests.Tests;

/// <summary>
/// Permission enforcement end-to-end (Task 8.1): a user with the right grant gets 200,
/// a user without it gets 403. Exercises the full RBAC pipeline: PermissionResolver
/// (direct + recursive entity grants, ProfileRights → "Module.Action") with the real
/// IDistributedCache (Redis container) — no mocks.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class PermissionEnforcementTests(IntegrationTestFixture fixture)
{
    private static string UniqueUsername() => $"it_perm_{Guid.NewGuid():N}";

    private static async Task<string> RegisterAndLoginAsync(
        HttpClient client, string username, string password = "Test-Passw0rd!2024")
    {
        await client.PostAsJsonAsync("api/v1/identity/auth/register", new
        {
            Username = username,
            Email = $"{username}@test.local",
            Password = password,
            DisplayName = "Permission Test User",
        });

        var login = await client.PostAsJsonAsync("api/v1/identity/auth/login", new
        {
            UsernameOrEmail = username,
            Password = password,
        });
        Assert.True(login.IsSuccessStatusCode, $"login failed: {login.StatusCode}");

        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task UserWithGrant_CanAccessProtectedEndpoint()
    {
        var client = fixture.Api.CreateClient();
        var username = UniqueUsername();

        // Register first so the password hash is real (PBKDF2), then attach the
        // entity + profile + right + grant and set DefaultEntityId directly.
        var token = await RegisterAndLoginAsync(client, username);
        await fixture.SeedPermissionGrantAsync(username, "reports", "read");

        // Re-login: the JWT must carry active_entity_id = entityId for the
        // permission handler to resolve grants at all.
        var scopedToken = await LoginAndGetTokenAsync(client, username);

        var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/reports/definitions");
        request.Headers.Authorization = new("Bearer", scopedToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> LoginAndGetTokenAsync(HttpClient client, string username)
    {
        var login = await client.PostAsJsonAsync("api/v1/identity/auth/login", new
        {
            UsernameOrEmail = username,
            Password = "Test-Passw0rd!2024",
        });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task UserWithoutGrant_Gets403OnProtectedEndpoint()
    {
        var client = fixture.Api.CreateClient();
        var username = UniqueUsername();

        // Registered + logged in, but no profile/right/grant anywhere → 403.
        var token = await RegisterAndLoginAsync(client, username);

        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var response = await client.GetAsync("api/v1/reports/definitions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserWithGrant_ForDifferentPermission_Gets403()
    {
        var client = fixture.Api.CreateClient();
        var username = UniqueUsername();

        // Grant "reports.read" but call an endpoint requiring "reports.manage".
        var token = await RegisterAndLoginAsync(client, username);
        await fixture.SeedPermissionGrantAsync(username, "reports", "read");

        var scopedToken = await LoginAndGetTokenAsync(client, username);
        client.DefaultRequestHeaders.Authorization = new("Bearer", scopedToken);

        var response = await client.PostAsJsonAsync("api/v1/reports/definitions", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
