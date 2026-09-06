using Daraban.Modules.Identity.Data;
using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Services.Authorization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Daraban.Modules.Identity.Tests;

/// <summary>
/// EntityScopeAccessor resolves which entity ids a caller's grant reaches, via a materialized-path
/// prefix match on EntityNode.FullPath (Task 1.2 SS9) rather than a recursive CTE. The prefix
/// approach is fast but has a sharp edge -- a sibling path that starts with the same characters
/// must not be swept in -- so that case is asserted explicitly.
/// </summary>
public class EntityScopeAccessorTests : IDisposable
{
    private readonly IdentityDbContext _db;

    public EntityScopeAccessorTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"entity-scope-{Guid.CreateVersion7()}")
            .Options;

        _db = new IdentityDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private EntityScopeAccessor CreateSut() => new(_db);

    private async Task<EntityNode> SeedAsync(string name, string fullPath, Guid? parentId = null)
    {
        var node = new EntityNode
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            FullPath = fullPath,
            ParentId = parentId,
        };

        _db.Entities.Add(node);
        await _db.SaveChangesAsync();
        return node;
    }

    [Fact]
    public async Task GetScopedEntityIdsAsync_Returns_Only_Self_When_Not_Recursive()
    {
        var root = await SeedAsync("Root", "/root/");
        await SeedAsync("Child", "/root/child/", root.Id);

        var scoped = await CreateSut().GetScopedEntityIdsAsync(root.Id, recursive: false);

        // A non-recursive grant applies to exactly one entity, so this must not even query the tree.
        Assert.Single(scoped);
        Assert.Contains(root.Id, scoped);
    }

    [Fact]
    public async Task GetScopedEntityIdsAsync_Includes_The_Whole_Subtree_When_Recursive()
    {
        var root = await SeedAsync("Root", "/root/");
        var child = await SeedAsync("Child", "/root/child/", root.Id);
        var grandchild = await SeedAsync("Grandchild", "/root/child/grandchild/", child.Id);

        var scoped = await CreateSut().GetScopedEntityIdsAsync(root.Id, recursive: true);

        Assert.Equal(3, scoped.Count);
        Assert.Contains(root.Id, scoped);
        Assert.Contains(child.Id, scoped);
        Assert.Contains(grandchild.Id, scoped);
    }

    [Fact]
    public async Task GetScopedEntityIdsAsync_Starts_From_The_Requested_Node_Not_The_Root()
    {
        var root = await SeedAsync("Root", "/root/");
        var child = await SeedAsync("Child", "/root/child/", root.Id);
        var grandchild = await SeedAsync("Grandchild", "/root/child/grandchild/", child.Id);
        await SeedAsync("Sibling", "/root/sibling/", root.Id);

        var scoped = await CreateSut().GetScopedEntityIdsAsync(child.Id, recursive: true);

        // A branch admin sees their branch, not the parent and not a sibling branch.
        Assert.Equal(2, scoped.Count);
        Assert.Contains(child.Id, scoped);
        Assert.Contains(grandchild.Id, scoped);
        Assert.DoesNotContain(root.Id, scoped);
    }

    [Fact]
    public async Task GetScopedEntityIdsAsync_Excludes_A_Sibling_Whose_Path_Shares_A_Prefix()
    {
        var root = await SeedAsync("Root", "/root/");
        var finance = await SeedAsync("Finance", "/root/finance/", root.Id);
        var financeReporting = await SeedAsync("Finance Reporting", "/root/finance-reporting/", root.Id);

        var scoped = await CreateSut().GetScopedEntityIdsAsync(finance.Id, recursive: true);

        // The trailing separator in FullPath is what makes the prefix match safe: without it,
        // "/root/finance" would also match "/root/finance-reporting/" and a Finance admin would
        // silently gain access to a sibling department.
        Assert.Single(scoped);
        Assert.Contains(finance.Id, scoped);
        Assert.DoesNotContain(financeReporting.Id, scoped);
    }

    [Fact]
    public async Task GetScopedEntityIdsAsync_Returns_Just_The_Id_For_An_Unknown_Entity()
    {
        await SeedAsync("Root", "/root/");
        var unknownId = Guid.CreateVersion7();

        var scoped = await CreateSut().GetScopedEntityIdsAsync(unknownId, recursive: true);

        // Fails closed: an unresolvable entity yields a useless single-id scope rather than widening
        // to everything, which is what an empty-or-all fallback would risk.
        Assert.Single(scoped);
        Assert.Contains(unknownId, scoped);
    }

    [Fact]
    public async Task GetScopedEntityIdsAsync_Handles_A_Leaf_Node()
    {
        var root = await SeedAsync("Root", "/root/");
        var leaf = await SeedAsync("Leaf", "/root/leaf/", root.Id);

        var scoped = await CreateSut().GetScopedEntityIdsAsync(leaf.Id, recursive: true);

        Assert.Single(scoped);
        Assert.Contains(leaf.Id, scoped);
    }

    [Fact]
    public async Task GetScopedEntityIdsAsync_Ignores_A_Separate_Root_Tree()
    {
        var first = await SeedAsync("Tenant A", "/tenant-a/");
        await SeedAsync("Tenant A Child", "/tenant-a/child/", first.Id);

        var second = await SeedAsync("Tenant B", "/tenant-b/");
        await SeedAsync("Tenant B Child", "/tenant-b/child/", second.Id);

        var scoped = await CreateSut().GetScopedEntityIdsAsync(first.Id, recursive: true);

        // Multi-tenant isolation at the entity-tree level.
        Assert.Equal(2, scoped.Count);
        Assert.DoesNotContain(second.Id, scoped);
    }
}
