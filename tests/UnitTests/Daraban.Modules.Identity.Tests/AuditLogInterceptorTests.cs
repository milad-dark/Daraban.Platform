using System.Text.Json;
using Daraban.Modules.Identity.Data;
using Daraban.Modules.Identity.Data.Auditing;
using Daraban.Modules.Identity.Data.Entities;
using Daraban.Platform.Common;
using Daraban.Platform.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Daraban.Modules.Identity.Tests;

/// <summary>
/// AuditLogSaveChangesInterceptor (Task 7.3) -- the write side of the audit trail. Run against
/// the real EF Core InMemory provider (not a mock) because the thing under test is what the
/// ChangeTracker actually reports and what actually gets written. Redaction is a security
/// control: if it ever regresses, password hashes and token hashes end up persisted in the
/// audit trail, so every case here pins an exact behavior.
/// </summary>
public class AuditLogInterceptorTests : IDisposable
{
    private readonly IdentityDbContext _db;

    public AuditLogInterceptorTests()
    {
        _db = CreateContext();
    }

    public void Dispose() => _db.Dispose();

    // ---- What gets audited -----------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_Writes_One_Audit_Row_Per_Tracked_Change()
    {
        _db.Users.Add(MakeUser("alice", isActive: true));
        _db.Profiles.Add(new Profile { Id = Guid.CreateVersion7(), Name = "Admins" });

        await _db.SaveChangesAsync();

        Assert.Equal(2, _db.AuditLogs.Local.Count);
    }

    [Fact]
    public async Task SaveChangesAsync_Records_The_Insert_As_Added_With_Only_New_Values()
    {
        var user = MakeUser("alice", isActive: true);
        _db.Users.Add(user);

        await _db.SaveChangesAsync();

        var audit = _db.AuditLogs.Local.Single(a => a.Action == "Added" && a.EntityType == "User");
        Assert.Equal("Added", audit.Action);
        Assert.Equal("User", audit.EntityType);
        Assert.Null(audit.OldValues);
        Assert.NotNull(audit.NewValues);
    }

    [Fact]
    public async Task SaveChangesAsync_Records_A_Modification_With_Both_Sides_Of_The_Diff()
    {
        var user = MakeUser("alice", isActive: true);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        user.IsActive = false;
        await _db.SaveChangesAsync();

        var audit = _db.AuditLogs.Local.Single(a => a.Action == "Modified");
        Assert.Equal("User", audit.EntityType);
        Assert.Equal(user.Id, audit.EntityId);
        Assert.Contains(@"""isActive"":true", audit.OldValues);
        Assert.Contains(@"""isActive"":false", audit.NewValues);
    }

    [Fact]
    public async Task SaveChangesAsync_Records_A_Deletion_With_Only_Old_Values()
    {
        var user = MakeUser("alice", isActive: true);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        var audit = _db.AuditLogs.Local.Single(a => a.Action == "Deleted");
        Assert.Null(audit.NewValues);
        Assert.NotNull(audit.OldValues);
    }

    // ---- What must never be audited ---------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_Does_Not_Audit_Its_Own_Audit_Rows()
    {
        _db.Users.Add(MakeUser("alice", isActive: true));

        await _db.SaveChangesAsync();
        await _db.SaveChangesAsync();

        // The audit row saved by the first pass is Unchanged by the second; auditing it (or
        // its own persistence) would recurse forever without the AuditLog self-exclusion.
        // After save two: one persisted audit row total, nothing newly queued.
        Assert.Equal(0, PendingAuditRowCount(_db));
        Assert.Equal(1, _db.AuditLogs.Local.Count);
    }

    [Fact]
    public async Task SaveChangesAsync_Does_Not_Audit_Unchanged_Entities()
    {
        var user = MakeUser("alice", isActive: true);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.Users.Add(MakeUser("bob", isActive: true));
        await _db.SaveChangesAsync();

        // Only bob's insert is audited -- alice's untouched row from the first save is not
        // re-audited (two rows total, none newly queued after the save completes).
        Assert.Equal(0, PendingAuditRowCount(_db));
        Assert.Equal(2, _db.AuditLogs.Local.Count);
    }

    [Fact]
    public async Task SaveChangesAsync_Honors_Excluded_Entity_Types_From_Configuration()
    {
        using var db = CreateContext(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Audit:ExcludedEntityTypes:0"] = "RefreshToken",
            }));

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            TokenHash = "sha256-of-token",
            FamilyId = Guid.CreateVersion7(),
            IssuedAt = DateTimeOffset.UtcNow,
            FamilyIssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(14),
        });

        await db.SaveChangesAsync();

        Assert.Empty(db.AuditLogs.Local);
    }

    // ---- Sensitive-value redaction (security controls) ----------------------------------------

    [Fact]
    public async Task SaveChangesAsync_Redacts_Password_Hashes()
    {
        var user = MakeUser("alice", isActive: true, passwordHash: "AQAAAAIAACcD-real-password-hash");
        _db.Users.Add(user);

        await _db.SaveChangesAsync();

        var audit = Assert.Single(_db.AuditLogs.Local);
        Assert.DoesNotContain("AQAAAAIAACcD", audit.NewValues);
        Assert.Contains(""" "passwordHash":"[REDACTED]" """.Trim(), audit.NewValues);
    }

    [Fact]
    public async Task SaveChangesAsync_Redacts_Token_Hashes()
    {
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            TokenHash = "base64-encoded-sha256-of-a-real-refresh-token",
            FamilyId = Guid.CreateVersion7(),
            IssuedAt = DateTimeOffset.UtcNow,
            FamilyIssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(14),
        });

        await _db.SaveChangesAsync();

        var audit = Assert.Single(_db.AuditLogs.Local);
        Assert.DoesNotContain("base64-encoded-sha256", audit.NewValues);
        Assert.Contains("[REDACTED]", audit.NewValues);
    }

    [Fact]
    public async Task SaveChangesAsync_Redacts_Client_Secret_Hashes()
    {
        var agentId = Guid.CreateVersion7();
        _db.Agents.Add(new Agent { Id = agentId, Name = "ws-01" });
        _db.AgentCredentials.Add(new AgentCredential
        {
            Id = Guid.CreateVersion7(),
            AgentId = agentId,
            ClientId = "public-client-id", // public on purpose -- must survive
            ClientSecretHash = "sha256-of-secret",
        });

        await _db.SaveChangesAsync();

        var audit = _db.AuditLogs.Local.Single(a => a.EntityType == "AgentCredential");
        Assert.DoesNotContain("sha256-of-secret", audit.NewValues);
        Assert.Contains(""" "clientId":"public-client-id" """.Trim(), audit.NewValues);
    }

    [Fact]
    public async Task SaveChangesAsync_Redacts_Foreign_Keys_That_Point_At_Secrets()
    {
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7(),
            TokenHash = "base64-encoded-sha256-of-a-real-refresh-token",
            FamilyId = Guid.CreateVersion7(),
            IssuedAt = DateTimeOffset.UtcNow,
            FamilyIssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(14),
        });

        await _db.SaveChangesAsync();

        var audit = Assert.Single(_db.AuditLogs.Local);
        // RefreshToken.UserId would map a token row to its owner -- a correlation the audit
        // reader has no other way to make, so the FK itself is redacted.
        using var json = JsonDocument.Parse(audit.NewValues!);
        Assert.Equal("[REDACTED-FK]", json.RootElement.GetProperty("userId").GetString());
    }

    // ---- Field hygiene -----------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_Omits_Bookkeeping_Columns_From_The_Snapshot()
    {
        _db.Users.Add(MakeUser("alice", isActive: true));

        await _db.SaveChangesAsync();

        var audit = Assert.Single(_db.AuditLogs.Local);
        Assert.DoesNotContain("createdAt", audit.NewValues);
        Assert.DoesNotContain("updatedAt", audit.NewValues);
    }

    // ---- Request context (IP / user-agent / actor) ---------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_Truncates_An_Oversized_User_Agent()
    {
        using var db = CreateContext(
            accessor: StubAccessor(ip: null, userAgent: new string('x', 10_000)));

        db.Users.Add(MakeUser("alice", isActive: true));
        await db.SaveChangesAsync();

        var audit = Assert.Single(db.AuditLogs.Local);
        Assert.Equal(512, audit.UserAgent!.Length);
    }

    [Fact]
    public async Task SaveChangesAsync_Captures_The_Request_Ip_And_User_Agent()
    {
        using var db = CreateContext(
            accessor: StubAccessor(ip: "10.1.2.3", userAgent: "Daraban-Agent/1.0"));

        db.Users.Add(MakeUser("alice", isActive: true));
        await db.SaveChangesAsync();

        var audit = Assert.Single(db.AuditLogs.Local);
        Assert.Equal("10.1.2.3", audit.IpAddress);
        Assert.Equal("Daraban-Agent/1.0", audit.UserAgent);
    }

    [Fact]
    public async Task SaveChangesAsync_Attributes_The_Change_To_The_Authenticated_User()
    {
        var userId = Guid.CreateVersion7();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.IsAuthenticated).Returns(true);

        using var db = CreateContext(accessor: StubAccessor(ip: null, userAgent: null, currentUser.Object));

        db.Users.Add(MakeUser("alice", isActive: true));
        await db.SaveChangesAsync();

        Assert.Equal(userId, Assert.Single(db.AuditLogs.Local).ActorUserId);
    }

    [Fact]
    public async Task SaveChangesAsync_Leaves_The_Actor_Empty_When_No_Request_Scope_Exists()
    {
        // The constructor-only path (no HTTP context at all) is the worker/service shape:
        // the change is a system change and must be attributed to nobody, not guessed.
        using var db = CreateContext();

        db.Users.Add(MakeUser("alice", isActive: true));
        await db.SaveChangesAsync();

        Assert.Null(Assert.Single(db.AuditLogs.Local).ActorUserId);
    }

    [Fact]
    public async Task SaveChangesAsync_Leaves_The_Actor_Empty_When_Unauthenticated()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(c => c.IsAuthenticated).Returns(false);

        using var db = CreateContext(accessor: StubAccessor(ip: null, userAgent: null, currentUser.Object));

        db.Users.Add(MakeUser("alice", isActive: true));
        await db.SaveChangesAsync();

        Assert.Null(Assert.Single(db.AuditLogs.Local).ActorUserId);
    }

    // ---- Helpers ---------------------------------------------------------------------------

    private static IdentityDbContext CreateContext(
        Action<ConfigurationBuilder>? config = null,
        IHttpContextAccessor? accessor = null)
    {
        var builder = new ConfigurationBuilder();
        config?.Invoke(builder);

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"audit-interceptor-{Guid.CreateVersion7()}")
            .AddInterceptors(new AuditLogSaveChangesInterceptor(accessor, builder.Build()))
            .Options;

        return new IdentityDbContext(options);
    }

    private static IHttpContextAccessor StubAccessor(string? ip, string? userAgent, ICurrentUser? currentUser = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = ip is null ? null : System.Net.IPAddress.Parse(ip);
        if (userAgent is not null)
        {
            context.Request.Headers.UserAgent = userAgent;
        }

        if (currentUser is not null)
        {
            // Mirrors how the hosts expose request-scoped services to the pipeline.
            context.RequestServices = new ServiceCollection()
                .AddSingleton(currentUser)
                .BuildServiceProvider();
        }

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(context);
        return accessor.Object;
    }

    private static int PendingAuditRowCount(IdentityDbContext db)
        => db.ChangeTracker.Entries<AuditLog>().Count(e => e.State == EntityState.Added);

    private static User MakeUser(string username, bool isActive, string? passwordHash = null)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Username = username,
            Email = $"{username}@daraban.test",
            DisplayName = username,
            PasswordHash = passwordHash ?? "AQAAAAIAACcD-default-hash",
            IsActive = isActive,
        };
}
