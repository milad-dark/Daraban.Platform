using System.IdentityModel.Tokens.Jwt;
using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Modules.Identity.Services.Agents;
using Daraban.Modules.Identity.Services.Auth;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Daraban.Modules.Identity.Tests;

/// <summary>
/// AgentAuthService: the OAuth2 client_credentials flow for machine principals. Scope resolution
/// is the sharp edge here -- an agent must never receive a scope it was not granted, and a partial
/// match must be refused outright rather than quietly downgraded.
/// </summary>
public class AgentAuthServiceTests
{
    private const string ClientId = "agent-client-id";
    private const string ClientSecret = "agent-client-secret-value";

    private readonly Mock<IAgentRepository> _repo = new(MockBehavior.Loose);
    private readonly Mock<IAgentService> _agentService = new(MockBehavior.Loose);
    private readonly JwtSigningKeyProvider _keyProvider;

    public AgentAuthServiceTests()
    {
        _keyProvider = new JwtSigningKeyProvider(
            Options.Create(new JwtOptions()),
            new DevelopmentHostEnvironment(),
            NullLogger<JwtSigningKeyProvider>.Instance);
    }

    private AgentAuthService CreateSut() => new(
        _repo.Object,
        _agentService.Object,
        Options.Create(new JwtOptions
        {
            Issuer = "https://daraban.test",
            Audience = "daraban-api",
            AccessTokenLifetimeMinutes = 15,
        }),
        _keyProvider);

    private static Agent AgentWith(
        string allowedScopes = "inventory:write,assets:read",
        AgentStatus status = AgentStatus.Active,
        Guid? entityId = null)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Name = "Inventory Scanner",
            Type = AgentType.InventoryScanner,
            Status = status,
            AllowedScopes = allowedScopes,
            EntityId = entityId,
        };

    private static AgentCredential CredentialFor(
        Agent agent,
        string? scopes = null,
        bool isActive = true,
        DateTimeOffset? expiresAt = null,
        string secret = ClientSecret)
        => new()
        {
            Id = Guid.CreateVersion7(),
            AgentId = agent.Id,
            ClientId = ClientId,
            ClientSecretHash = AgentService.HashSecret(secret),
            Scopes = scopes,
            IsActive = isActive,
            ExpiresAt = expiresAt,
            Agent = agent,
        };

    private void ArrangeCredential(AgentCredential? credential)
        => _repo.Setup(r => r.GetCredentialByClientIdAsync(ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credential);

    private static JwtSecurityToken Parse(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    // ---- Credential checks -------------------------------------------------------------------

    [Fact]
    public async Task GetTokenAsync_Rejects_An_Unknown_ClientId()
    {
        ArrangeCredential(null);

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest("ghost", ClientSecret, null), null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal("AGENTS.AUTH_INVALID_CREDENTIALS", result.Error!.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task GetTokenAsync_Returns_The_Same_Error_For_Unknown_Client_And_Wrong_Secret()
    {
        // Unknown client.
        ArrangeCredential(null);
        var unknown = await CreateSut().GetTokenAsync(
            new TokenRequest("ghost", ClientSecret, null), null, null);

        // Known client, wrong secret.
        var agent = AgentWith();
        ArrangeCredential(CredentialFor(agent));
        var wrongSecret = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, "not-the-secret", null), null, null);

        // Identical code and message, or the response becomes a client_id enumeration oracle.
        Assert.Equal(unknown.Error!.Code, wrongSecret.Error!.Code);
        Assert.Equal(unknown.Error.Message, wrongSecret.Error.Message);
    }

    [Fact]
    public async Task GetTokenAsync_Rejects_A_Revoked_Credential()
    {
        var agent = AgentWith();
        ArrangeCredential(CredentialFor(agent, isActive: false));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, null), null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal("AGENTS.AUTH_CREDENTIAL_REVOKED", result.Error!.Code);
    }

    [Fact]
    public async Task GetTokenAsync_Rejects_An_Expired_Credential()
    {
        var agent = AgentWith();
        ArrangeCredential(CredentialFor(agent, expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1)));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, null), null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal("AGENTS.AUTH_CREDENTIAL_EXPIRED", result.Error!.Code);
    }

    [Fact]
    public async Task GetTokenAsync_Accepts_A_Credential_With_No_Expiry()
    {
        var agent = AgentWith();
        ArrangeCredential(CredentialFor(agent, expiresAt: null));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, null), null, null);

        // NULL expiry means "does not expire" -- long-lived agent credentials are the norm.
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(AgentStatus.Deactivated)]
    [InlineData(AgentStatus.Suspended)]
    public async Task GetTokenAsync_Rejects_A_Non_Active_Agent(AgentStatus status)
    {
        var agent = AgentWith(status: status);
        ArrangeCredential(CredentialFor(agent));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, null), null, null);

        // Disabling the agent has to be enough to shut it out, without hunting down every
        // credential it owns.
        Assert.False(result.IsSuccess);
        Assert.Equal("AGENTS.AUTH_AGENT_INACTIVE", result.Error!.Code);
    }

    [Fact]
    public async Task GetTokenAsync_Rejects_A_Credential_Whose_Agent_Was_SoftDeleted()
    {
        var credential = CredentialFor(AgentWith());
        // The soft-delete query filter excludes the agent from the Include, leaving the navigation
        // unset -- which must be treated as "not active", not dereferenced.
        credential.Agent = null!;
        ArrangeCredential(credential);

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, null), null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal("AGENTS.AUTH_AGENT_INACTIVE", result.Error!.Code);
    }

    // ---- Scope resolution --------------------------------------------------------------------

    [Fact]
    public async Task GetTokenAsync_Grants_Every_Available_Scope_When_None_Is_Requested()
    {
        var agent = AgentWith("inventory:write,assets:read");
        ArrangeCredential(CredentialFor(agent));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, null), null, null);

        Assert.True(result.IsSuccess);
        Assert.Contains("inventory:write", result.Value.Scope);
        Assert.Contains("assets:read", result.Value.Scope);
    }

    [Fact]
    public async Task GetTokenAsync_Grants_Exactly_The_Requested_Subset()
    {
        var agent = AgentWith("inventory:write,assets:read");
        ArrangeCredential(CredentialFor(agent));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, "assets:read"), null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("assets:read", result.Value.Scope);

        // Least privilege: asking for one scope must not hand over both.
        Assert.DoesNotContain("inventory:write", result.Value.Scope);
    }

    [Fact]
    public async Task GetTokenAsync_Refuses_A_Scope_The_Agent_Does_Not_Have()
    {
        var agent = AgentWith("assets:read");
        ArrangeCredential(CredentialFor(agent));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, "assets:delete"), null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal("AGENTS.AUTH_SCOPE_DENIED", result.Error!.Code);
    }

    [Fact]
    public async Task GetTokenAsync_Refuses_A_Partial_Scope_Match_Rather_Than_Downgrading()
    {
        var agent = AgentWith("assets:read");
        ArrangeCredential(CredentialFor(agent));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, "assets:read assets:delete"),
            null, null);

        // Silently granting the half that was permitted would leave the agent believing it holds
        // both, and failing in a confusing way later. An explicit denial is the honest answer.
        Assert.False(result.IsSuccess);
        Assert.Equal("AGENTS.AUTH_SCOPE_DENIED", result.Error!.Code);
    }

    [Fact]
    public async Task GetTokenAsync_Narrows_To_The_Credentials_Own_Scope_Limit()
    {
        // The agent may do both; this particular credential is limited to reading.
        var agent = AgentWith("inventory:write,assets:read");
        ArrangeCredential(CredentialFor(agent, scopes: "assets:read"));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, "inventory:write"), null, null);

        // Per-credential narrowing is how one agent can hold a read-only key for a low-trust host.
        Assert.False(result.IsSuccess);
        Assert.Equal("AGENTS.AUTH_SCOPE_DENIED", result.Error!.Code);
    }

    [Fact]
    public async Task GetTokenAsync_Expands_A_Wildcard_Credential_To_The_Agents_Allowed_Scopes()
    {
        var agent = AgentWith("inventory:write,assets:read");
        ArrangeCredential(CredentialFor(agent, scopes: "*"));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, "inventory:write"), null, null);

        // A wildcard means "everything this agent may do" -- never more than the agent itself has.
        Assert.True(result.IsSuccess);
        Assert.Equal("inventory:write", result.Value.Scope);
    }

    [Fact]
    public async Task GetTokenAsync_Matches_Scopes_Case_Insensitively()
    {
        var agent = AgentWith("Inventory:Write");
        ArrangeCredential(CredentialFor(agent));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, "inventory:write"), null, null);

        Assert.True(result.IsSuccess);
    }

    // ---- Issued token ------------------------------------------------------------------------

    [Fact]
    public async Task GetTokenAsync_Issues_A_Bearer_Token_With_The_Agent_Claims()
    {
        var entityId = Guid.CreateVersion7();
        var agent = AgentWith(entityId: entityId);
        ArrangeCredential(CredentialFor(agent));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, null), null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("Bearer", result.Value.TokenType);
        Assert.Equal(900, result.Value.ExpiresIn); // 15 minutes

        var parsed = Parse(result.Value.AccessToken);
        Assert.Equal(agent.Id.ToString(), parsed.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal("InventoryScanner", parsed.Claims.Single(c => c.Type == "agent_type").Value);
        Assert.Equal("true", parsed.Claims.Single(c => c.Type == "is_agent").Value);
        Assert.Equal(entityId.ToString(), parsed.Claims.Single(c => c.Type == "active_entity_id").Value);
        Assert.Equal(SecurityAlgorithms.RsaSha256, parsed.Header.Alg);
    }

    [Fact]
    public async Task GetTokenAsync_Omits_Token_Version_For_Agents()
    {
        var agent = AgentWith();
        ArrangeCredential(CredentialFor(agent));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, null), null, null);

        // Agent tokens are revoked by deactivating the credential or the agent, not by bumping a
        // per-user version counter -- there is no user record behind them.
        Assert.DoesNotContain("token_version", Parse(result.Value.AccessToken).Claims.Select(c => c.Type));
    }

    [Fact]
    public async Task GetTokenAsync_Never_Embeds_The_Secret_Hash()
    {
        var agent = AgentWith();
        var credential = CredentialFor(agent);
        ArrangeCredential(credential);

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, null), null, null);

        // A JWT payload is base64, not encryption.
        Assert.DoesNotContain(credential.ClientSecretHash, result.Value.AccessToken);
        Assert.DoesNotContain(ClientSecret, result.Value.AccessToken);
    }

    [Fact]
    public async Task GetTokenAsync_Touches_LastUsed_And_LastActive()
    {
        var agent = AgentWith();
        var credential = CredentialFor(agent);
        ArrangeCredential(credential);

        await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, null), null, null);

        // Backs the "last seen" column on the agent dashboard.
        Assert.NotNull(credential.LastUsedAt);
        Assert.NotNull(agent.LastActiveAt);
    }

    [Fact]
    public async Task GetTokenAsync_Still_Issues_A_Token_When_The_Audit_Write_Fails()
    {
        var agent = AgentWith();
        ArrangeCredential(CredentialFor(agent));

        _agentService.Setup(s => s.LogActionAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<long?>(), It.IsAny<bool>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit store unavailable"));

        var result = await CreateSut().GetTokenAsync(
            new TokenRequest(ClientId, ClientSecret, null), null, null);

        // The token has already been minted by this point; failing the request would be worse than
        // losing one audit row, and the agent would retry and mint another anyway.
        Assert.True(result.IsSuccess);
    }

    /// <summary>Development environment so JwtSigningKeyProvider self-generates a key.</summary>
    private sealed class DevelopmentHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Daraban.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
