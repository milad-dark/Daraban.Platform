// Both hosts expose a global-namespace 'Program' (top-level statements); the AgentApi
// reference is aliased (see csproj) so its entry point can be addressed unambiguously.
extern alias AgentApiHost;
using Daraban.Modules.Identity.Data;
using Daraban.Modules.Identity.Data.Entities;
using Microsoft.AspNetCore.Builder; // AddRateLimiter
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http; // StatusCodes
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting; // RateLimiterOptions
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options; // IConfigureOptions
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace Daraban.IntegrationTests;

/// <summary>
/// Shared infrastructure for all integration tests (Task 8.1).
///
/// Boots three real containers (PostgreSQL + Redis + RabbitMQ) once per test run,
/// creates every module's database schema by executing each DbContext's
/// GenerateCreateScript() against the fresh database, then exposes two
/// WebApplicationFactory instances (Host.Api and Host.AgentApi) wired to those
/// containers.
///
/// Schema strategy: no host calls Migrate()/EnsureCreated() at startup, and EF's
/// EnsureCreated skips ALL creation once the database has ANY table — so the
/// fixture pre-creates all schemas and runs each context's create script
/// explicitly BEFORE the factories start (the hosts run seeders at startup whose
/// raw DDL would otherwise make EnsureCreated bail out).
/// </summary>
public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:latest")
        .Build();
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis")
        .Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:management-alpine")
        .Build();

    private WebApplicationFactory<Program> _api = null!;
    private WebApplicationFactory<AgentApiHost::Program> _agentApi = null!;

    public WebApplicationFactory<Program> Api => _api;
    public WebApplicationFactory<AgentApiHost::Program> AgentApi => _agentApi;

    public string PostgresConnectionString => _postgres.GetConnectionString();

    /// <summary>Scoped service-provider access for direct DbContext seeding/verification.</summary>
    public IServiceScope CreateApiScope() => _api.Services.CreateScope();

    public async Task InitializeAsync()
    {
        // 1. Start containers (parallel — they are independent)
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _rabbitMq.StartAsync());

        // 2. Create all module schemas on the fresh database BEFORE any factory starts.
        await CreateDatabaseSchemaAsync();

        // 3. Build both host factories against the containers
        _api = CreateApiFactory();
        _agentApi = CreateAgentApiFactory();
    }

    public async Task DisposeAsync()
    {
        if (_api is not null)
            await _api.DisposeAsync();
        if (_agentApi is not null)
            await _agentApi.DisposeAsync();
        await _rabbitMq.DisposeAsync();
        await _redis.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ------------------------------------------------------------------
    // Schema creation
    // ------------------------------------------------------------------

    private async Task CreateDatabaseSchemaAsync()
    {
        // Every module owns one schema; Plugins/Settings/Identity map a few tables into
        // the cross-cutting "core" schema via ToTable(..., "core").
        var schemas = new[]
        {
            "assets", "automation", "dashboard", "discovery", "financial", "identity",
            "inventory", "knowledge", "notifications", "plugins", "reporting",
            "servicedesk", "settings", "software", "core"
        };

        await using var connection = new NpgsqlConnection(PostgresConnectionString);
        await connection.OpenAsync();

        foreach (var schema in schemas)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"";
            await cmd.ExecuteNonQueryAsync();
        }

        // Execute each module context's create script. Contexts own distinct schemas,
        // so the scripts never collide.
        var contextTypes = new[]
        {
            typeof(Daraban.Modules.Assets.Data.AssetsDbContext),
            typeof(Daraban.Modules.Automation.Data.AutomationDbContext),
            typeof(Daraban.Modules.Dashboard.Data.DashboardDbContext),
            typeof(Daraban.Modules.Discovery.Data.DiscoveryDbContext),
            typeof(Daraban.Modules.Financial.Data.FinancialDbContext),
            typeof(IdentityDbContext),
            typeof(Daraban.Modules.Inventory.Data.InventoryDbContext),
            typeof(Daraban.Modules.Knowledge.Data.KnowledgeDbContext),
            typeof(Daraban.Modules.Notifications.Data.NotificationsDbContext),
            typeof(Daraban.Modules.Plugins.Data.PluginsDbContext),
            typeof(Daraban.Modules.Reporting.Data.ReportingDbContext),
            typeof(Daraban.Modules.ServiceDesk.Data.ServiceDeskDbContext),
            typeof(Daraban.Modules.Settings.Data.SettingsDbContext),
            typeof(Daraban.Modules.Software.Data.SoftwareDbContext),
        };

        foreach (var contextType in contextTypes)
        {
            // Every module context exposes a single ctor taking DbContextOptions<TContext>;
            // build the matching generic options via reflection (MakeGenericMethod on
            // UseNpgsql) so Activator finds the right constructor.
            var optionsMethod = typeof(IntegrationTestFixture)
                .GetMethod(nameof(BuildOptionsFor), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(contextType);
            var options = (DbContextOptions)optionsMethod.Invoke(null, new object[] { PostgresConnectionString })!;
            using var context = (DbContext)Activator.CreateInstance(contextType, options)!;
            var script = context.Database.GenerateCreateScript();

            // Empty contexts (Automation/Notifications have no entities yet) produce
            // empty scripts — executing a blank command would throw.
            if (string.IsNullOrWhiteSpace(script))
                continue;

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = script;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static DbContextOptions<T> BuildOptionsFor<T>(string connectionString) where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseNpgsql(connectionString)
            .Options;

    // ------------------------------------------------------------------
    // Factory configuration
    // ------------------------------------------------------------------

    private WebApplicationFactory<Program> CreateApiFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            ApplyContainerSettings(builder);
            builder.ConfigureTestServices(services =>
            {
                // The test server shares the loopback IP — neutralize the per-IP rate
                // limiter so parallel tests don't trip 429s. AddRateLimiter registers
                // IConfigureOptions<RateLimiterOptions> (not a RateLimiterOptions
                // descriptor), so strip those configurator registrations and re-add
                // permissive replacements. The named policies must still exist because
                // [EnableRateLimiting("auth")] throws at request time when its policy
                // is missing — but with effectively unlimited permits they never trip.
                var configurators = services
                    .Where(d => d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>)
                        && d.ServiceType.GetGenericArguments()[0] == typeof(RateLimiterOptions))
                    .ToList();
                foreach (var d in configurators)
                    services.Remove(d);
                services.AddRateLimiter(o =>
                {
                    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                    o.AddFixedWindowLimiter("auth", opt =>
                    {
                        opt.PermitLimit = int.MaxValue;
                        opt.Window = TimeSpan.FromSeconds(1);
                        opt.QueueLimit = 0;
                    });
                    o.AddFixedWindowLimiter("discovery-scan", opt =>
                    {
                        opt.PermitLimit = int.MaxValue;
                        opt.Window = TimeSpan.FromSeconds(1);
                        opt.QueueLimit = 0;
                    });
                });
            });
        });

    private WebApplicationFactory<AgentApiHost::Program> CreateAgentApiFactory() =>
        new WebApplicationFactory<AgentApiHost::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            ApplyContainerSettings(builder);
        });

    private void ApplyContainerSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", PostgresConnectionString);
        // Keep the report file store out of /var/daraban (unwritable for a non-root CI user on
        // Linux): the shared hosts bind ReportStore:RootPath to that path via appsettings.json.
        builder.UseSetting("ReportStore:RootPath", Path.Combine(Path.GetTempPath(), $"daraban-itests-reports-{Guid.NewGuid():N}"));
        builder.UseSetting("ConnectionStrings:Redis", $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)}");
        builder.UseSetting("RabbitMq:Host", _rabbitMq.Hostname);
        builder.UseSetting("RabbitMq:Port", _rabbitMq.GetMappedPublicPort(5672).ToString());
        // Testcontainers.RabbitMq 4.x does not expose the credentials on the container;
        // the builder defaults (rabbitmq/rabbitmq) are what the image is configured with.
        builder.UseSetting("RabbitMq:Username", RabbitMqBuilder.DefaultUsername);
        builder.UseSetting("RabbitMq:Password", RabbitMqBuilder.DefaultPassword);
        // Force the ephemeral JWT signing key (appsettings.Development.json points at
        // a local certs file that does not exist in CI/test environments).
        builder.UseSetting("Jwt:SigningKeyPemPath", "");
    }

    // ------------------------------------------------------------------
    // Seeding helpers (direct DbContext access — bypasses the API surface)
    // ------------------------------------------------------------------

    /// <summary>Attaches an entity node + profile + right + grant to an ALREADY
    /// REGISTERED user (real PBKDF2 password hash) and sets DefaultEntityId, so the
    /// next login's JWT carries active_entity_id and the permission resolver can
    /// resolve the grant. Returns the entity id.</summary>
    public async Task<Guid> SeedPermissionGrantAsync(string username, string module, string action)
    {
        var entityId = Guid.NewGuid();

        using var scope = CreateApiScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var user = await db.Users.SingleAsync(u => u.Username == username);
        user.DefaultEntityId = entityId;

        db.Entities.Add(new EntityNode
        {
            Id = entityId,
            Name = $"Test Entity {entityId}",
            FullPath = $"/{entityId}/",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            // Profile.Name has a unique index — include the entity id so repeated
            // grants (and parallel tests) never collide.
            Name = $"Test Profile {module}.{action} {entityId}",
            IsDefault = false,
        };
        db.Profiles.Add(profile);

        db.ProfileRights.Add(new ProfileRight
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            Module = module,
            Action = action,
            IsRecursive = true,
        });

        db.UserProfileEntities.Add(new UserProfileEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProfileId = profile.Id,
            EntityId = entityId,
            IsRecursive = true,
            IsDefault = true,
        });

        await db.SaveChangesAsync();
        return entityId;
    }

    /// <summary>Registers an agent + credential directly in the identity schema.
    /// Cross-host JWT keys differ per factory, so agent seeding bypasses
    /// AgentManagementController (which requires a user JWT from Host.Api).</summary>
    public async Task<(Guid AgentId, string ClientId, string ClientSecret)> SeedAgentAsync(string name, string allowedScopes)
    {
        var agentId = Guid.NewGuid();
        var clientId = $"da_{Guid.NewGuid():N}";
        var clientSecret = $"sk_{Guid.NewGuid():N}{Guid.NewGuid():N}";

        using var scope = CreateApiScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var agent = new Agent
        {
            Id = agentId,
            Name = name,
            Description = "Integration test agent",
            Type = AgentType.InventoryScanner,
            Status = AgentStatus.Active,
            AllowedScopes = allowedScopes,
            RateLimitPerMinute = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Agents.Add(agent);

        db.AgentCredentials.Add(new AgentCredential
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            ClientId = clientId,
            // Same algorithm as AgentService.HashSecret (internal): SHA-256 UTF-8 → lowercase hex.
            ClientSecretHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(clientSecret))).ToLowerInvariant(),
            Label = "integration-test",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();
        return (agentId, clientId, clientSecret);
    }
}

/// <summary>Runs all integration tests against the single shared fixture (one container
/// set per test run — containers are expensive; tests must be independent of order).</summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "Integration";
}
