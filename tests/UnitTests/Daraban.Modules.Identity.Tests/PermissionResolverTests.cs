using System.Text.Json;
using Daraban.Modules.Identity.Data;
using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Services.Authorization;
using Daraban.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;

namespace Daraban.Modules.Identity.Tests;

/// <summary>
/// PermissionResolver against an in-memory-provider DbContext plus a real
/// <see cref="MemoryDistributedCache"/>. This is the RBAC algorithm from Task 1.3 SS4.3 -- direct
/// grants, recursive grants down the entity tree, and the cache that keeps it off the hot path.
/// Every authorized request funnels through here, so a false positive is a privilege escalation.
/// </summary>
public class PermissionResolverTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RootEntityId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ChildEntityId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid UnrelatedEntityId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly IdentityDbContext _db;
    private readonly IDistributedCache _cache;

    public PermissionResolverTests()
    {
        // A distinct database name per test instance -- xUnit runs test classes in parallel and a
        // shared in-memory store would leak grants between cases.
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"perm-resolver-{Guid.CreateVersion7()}")
            .Options;

        _db = new IdentityDbContext(options);
        _cache = new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(
                new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()));
    }

    public void Dispose() => _db.Dispose();

    private PermissionResolver CreateSut(IEntityScopeAccessor? scope = null)
        => new(_db, scope ?? StubScope(), _cache);

    /// <summary>Entity scope where RootEntityId's subtree is {Root, Child}.</summary>
    private static IEntityScopeAccessor StubScope()
    {
        var mock = new Mock<IEntityScopeAccessor>();
        mock.Setup(s => s.GetScopedEntityIdsAsync(RootEntityId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { RootEntityId, ChildEntityId });
        mock.Setup(s => s.GetScopedEntityIdsAsync(ChildEntityId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ChildEntityId });
        mock.Setup(s => s.GetScopedEntityIdsAsync(UnrelatedEntityId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { UnrelatedEntityId });
        return mock.Object;
    }

    private async Task<Guid> SeedProfileAsync(params (string Module, string Action)[] rights)
    {
        var profileId = Guid.CreateVersion7();
        _db.Profiles.Add(new Profile { Id = profileId, Name = $"Profile-{profileId:N}" });

        foreach (var (module, action) in rights)
        {
            _db.ProfileRights.Add(new ProfileRight
            {
                Id = Guid.CreateVersion7(),
                ProfileId = profileId,
                Module = module,
                Action = action,
            });
        }

        await _db.SaveChangesAsync();
        return profileId;
    }

    private async Task GrantAsync(Guid userId, Guid profileId, Guid entityId, bool isRecursive = false)
    {
        _db.UserProfileEntities.Add(new UserProfileEntity
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ProfileId = profileId,
            EntityId = entityId,
            IsRecursive = isRecursive,
        });
        await _db.SaveChangesAsync();
    }

    // ---- Direct grants -----------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_Returns_Nothing_For_A_User_With_No_Grants()
    {
        var permissions = await CreateSut().ResolveAsync(UserId, RootEntityId);

        // Closed by default: no grant means no permission, never an empty-set-means-allow.
        Assert.Empty(permissions);
    }

    [Fact]
    public async Task ResolveAsync_Returns_Module_Dot_Action_Strings()
    {
        var profileId = await SeedProfileAsync(("assets", "read"), ("assets", "write"));
        await GrantAsync(UserId, profileId, RootEntityId);

        var permissions = await CreateSut().ResolveAsync(UserId, RootEntityId);

        // This is the exact shape [RequirePermission("assets.write")] checks against.
        Assert.Contains("assets.read", permissions);
        Assert.Contains("assets.write", permissions);
        Assert.Equal(2, permissions.Count);
    }

    [Fact]
    public async Task ResolveAsync_Unions_Rights_Across_Multiple_Profiles_In_The_Same_Entity()
    {
        var reader = await SeedProfileAsync(("assets", "read"));
        var ticketWriter = await SeedProfileAsync(("servicedesk", "write"));
        await GrantAsync(UserId, reader, RootEntityId);
        await GrantAsync(UserId, ticketWriter, RootEntityId);

        var permissions = await CreateSut().ResolveAsync(UserId, RootEntityId);

        // A user can hold several profiles in one entity; the effective set is their union.
        Assert.Contains("assets.read", permissions);
        Assert.Contains("servicedesk.write", permissions);
    }

    [Fact]
    public async Task ResolveAsync_Deduplicates_A_Right_Granted_By_Two_Profiles()
    {
        var first = await SeedProfileAsync(("assets", "read"));
        var second = await SeedProfileAsync(("assets", "read"));
        await GrantAsync(UserId, first, RootEntityId);
        await GrantAsync(UserId, second, RootEntityId);

        var permissions = await CreateSut().ResolveAsync(UserId, RootEntityId);

        Assert.Single(permissions);
    }

    [Fact]
    public async Task ResolveAsync_Ignores_Grants_Belonging_To_Another_User()
    {
        var profileId = await SeedProfileAsync(("assets", "admin"));
        await GrantAsync(OtherUserId, profileId, RootEntityId);

        var permissions = await CreateSut().ResolveAsync(UserId, RootEntityId);

        Assert.Empty(permissions);
    }

    [Fact]
    public async Task ResolveAsync_Ignores_A_Non_Recursive_Grant_In_A_Different_Entity()
    {
        var profileId = await SeedProfileAsync(("assets", "read"));
        await GrantAsync(UserId, profileId, UnrelatedEntityId, isRecursive: false);

        var permissions = await CreateSut().ResolveAsync(UserId, RootEntityId);

        // Tenant isolation: a grant in one entity must not leak into another.
        Assert.Empty(permissions);
    }

    // ---- Recursive grants ---------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_Applies_A_Recursive_Grant_To_A_Descendant_Entity()
    {
        var profileId = await SeedProfileAsync(("assets", "read"));
        await GrantAsync(UserId, profileId, RootEntityId, isRecursive: true);

        var permissions = await CreateSut().ResolveAsync(UserId, ChildEntityId);

        // "Holds this profile in Root and every entity beneath it" -- the whole point of the
        // IsRecursive flag.
        Assert.Contains("assets.read", permissions);
    }

    [Fact]
    public async Task ResolveAsync_Does_Not_Apply_A_Recursive_Grant_Upwards()
    {
        var profileId = await SeedProfileAsync(("assets", "read"));
        await GrantAsync(UserId, profileId, ChildEntityId, isRecursive: true);

        var permissions = await CreateSut().ResolveAsync(UserId, RootEntityId);

        // Recursion flows down the tree, never up -- a branch admin is not an org admin.
        Assert.Empty(permissions);
    }

    [Fact]
    public async Task ResolveAsync_Does_Not_Apply_A_Recursive_Grant_Sideways()
    {
        var profileId = await SeedProfileAsync(("assets", "read"));
        await GrantAsync(UserId, profileId, UnrelatedEntityId, isRecursive: true);

        var permissions = await CreateSut().ResolveAsync(UserId, ChildEntityId);

        Assert.Empty(permissions);
    }

    [Fact]
    public async Task ResolveAsync_Combines_A_Direct_And_A_Recursive_Grant()
    {
        var orgWide = await SeedProfileAsync(("assets", "read"));
        var localOnly = await SeedProfileAsync(("assets", "write"));

        await GrantAsync(UserId, orgWide, RootEntityId, isRecursive: true);
        await GrantAsync(UserId, localOnly, ChildEntityId);

        var permissions = await CreateSut().ResolveAsync(UserId, ChildEntityId);

        // Read everywhere, write only in this branch -- the common real-world shape.
        Assert.Contains("assets.read", permissions);
        Assert.Contains("assets.write", permissions);
    }

    [Fact]
    public async Task ResolveAsync_Supports_The_Finer_Own_Group_All_Action_Form()
    {
        var profileId = await SeedProfileAsync(("servicedesk", "tickets.read.own"));
        await GrantAsync(UserId, profileId, RootEntityId);

        var permissions = await CreateSut().ResolveAsync(UserId, RootEntityId);

        // Action is free-form text, so GLPI's own/group/all distinction survives concatenation.
        Assert.Contains("servicedesk.tickets.read.own", permissions);
    }

    // ---- Caching -----------------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_Caches_The_Result()
    {
        var profileId = await SeedProfileAsync(("assets", "read"));
        await GrantAsync(UserId, profileId, RootEntityId);

        var sut = CreateSut();
        await sut.ResolveAsync(UserId, RootEntityId);

        var cached = await _cache.GetStringAsync($"perms:{UserId}:{RootEntityId}");

        // Every authorized request resolves permissions; without the cache that is a database
        // round trip per request per user.
        Assert.NotNull(cached);
        Assert.Contains("assets.read", JsonSerializer.Deserialize<HashSet<string>>(cached!)!);
    }

    [Fact]
    public async Task ResolveAsync_Serves_A_Second_Call_From_The_Cache()
    {
        var profileId = await SeedProfileAsync(("assets", "read"));
        await GrantAsync(UserId, profileId, RootEntityId);

        var sut = CreateSut();
        await sut.ResolveAsync(UserId, RootEntityId);

        // Revoke everything at the database level; the cached answer should still be served.
        _db.UserProfileEntities.RemoveRange(_db.UserProfileEntities);
        await _db.SaveChangesAsync();

        var second = await sut.ResolveAsync(UserId, RootEntityId);

        // Documents the trade-off: a rights change is not visible until the entry is invalidated or
        // the 5-minute TTL lapses. InvalidateAsync exists precisely for the former.
        Assert.Contains("assets.read", second);
    }

    [Fact]
    public async Task InvalidateAsync_Forces_The_Next_Resolve_To_Re_Read()
    {
        var profileId = await SeedProfileAsync(("assets", "read"));
        await GrantAsync(UserId, profileId, RootEntityId);

        var sut = CreateSut();
        await sut.ResolveAsync(UserId, RootEntityId);

        _db.UserProfileEntities.RemoveRange(_db.UserProfileEntities);
        await _db.SaveChangesAsync();
        await sut.InvalidateAsync(UserId, RootEntityId);

        var afterRevoke = await sut.ResolveAsync(UserId, RootEntityId);

        // This is what makes a revocation take effect on the next request instead of up to five
        // minutes later.
        Assert.Empty(afterRevoke);
    }

    [Fact]
    public async Task Cache_Entries_Are_Keyed_Per_User_And_Entity()
    {
        var readOnly = await SeedProfileAsync(("assets", "read"));
        var writeOnly = await SeedProfileAsync(("assets", "write"));

        await GrantAsync(UserId, readOnly, RootEntityId);
        await GrantAsync(UserId, writeOnly, UnrelatedEntityId);

        var sut = CreateSut();
        var inRoot = await sut.ResolveAsync(UserId, RootEntityId);
        var inUnrelated = await sut.ResolveAsync(UserId, UnrelatedEntityId);

        // A shared key would let a permission from one entity answer for another -- a cross-tenant
        // privilege escalation via the cache.
        Assert.Contains("assets.read", inRoot);
        Assert.DoesNotContain("assets.write", inRoot);
        Assert.Contains("assets.write", inUnrelated);
        Assert.DoesNotContain("assets.read", inUnrelated);
    }

    [Fact]
    public async Task InvalidateAsync_Is_Safe_When_Nothing_Is_Cached()
    {
        await CreateSut().InvalidateAsync(UserId, RootEntityId);
    }
}
