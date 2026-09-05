using Daraban.Modules.Knowledge.Data.Entities;
using Daraban.Modules.Knowledge.Data.Repositories;
using Daraban.Modules.Knowledge.Services;
using Daraban.Modules.Knowledge.Services.Dtos;
using Moq;
using Xunit;

namespace Daraban.Modules.Knowledge.Tests;

/// <summary>
/// KbCategoryService with mocked repositories: slug derivation, cycle rejection on re-parent,
/// and the refusal to delete a category that still owns children or articles.
/// </summary>
public class KbCategoryServiceTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IKbCategoryRepository> _repo = new(MockBehavior.Strict);

    private KbCategoryService CreateSut() => new(_repo.Object);

    private static KbCategory Category(Guid? id = null, Guid? parentId = null, string name = "Network")
        => new()
        {
            Id = id ?? Guid.CreateVersion7(),
            EntityId = EntityId,
            ParentId = parentId,
            Name = name,
            Slug = KbSlugProbe(name),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    // KbSlug is internal to the Services assembly; recompute the expected value the same way
    // rather than widening its visibility just for a test.
    private static string KbSlugProbe(string value) => value.ToLowerInvariant().Replace(' ', '-');

    [Fact]
    public async Task CreateAsync_Derives_A_Slug_From_The_Name_When_None_Is_Given()
    {
        KbCategory? captured = null;
        _repo.Setup(r => r.SlugExistsAsync("network-devices", EntityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.AddAsync(It.IsAny<KbCategory>(), It.IsAny<CancellationToken>()))
            .Callback<KbCategory, CancellationToken>((c, _) => captured = c)
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var request = new CreateKbCategoryRequest(null, "Network Devices", null, null);
        var result = await CreateSut().CreateAsync(request, EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("network-devices", captured!.Slug);
    }

    [Fact]
    public async Task CreateAsync_Strips_Accents_And_Punctuation_From_The_Slug()
    {
        _repo.Setup(r => r.SlugExistsAsync("reseau-vpn", EntityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.AddAsync(It.IsAny<KbCategory>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var request = new CreateKbCategoryRequest(null, "Réseau / VPN!", null, null);
        var result = await CreateSut().CreateAsync(request, EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("reseau-vpn", result.Value.Slug);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_Name_With_No_Sluggable_Characters()
    {
        var request = new CreateKbCategoryRequest(null, "!!!", null, null);
        var result = await CreateSut().CreateAsync(request, EntityId, ActorId);

        // "!!!" would slug to the empty string and then collide with every other unsluggable name.
        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.CATEGORY_SLUG_INVALID", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_Duplicate_Slug_Within_The_Same_Entity()
    {
        _repo.Setup(r => r.SlugExistsAsync("network", EntityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateKbCategoryRequest(null, "Network", null, null);
        var result = await CreateSut().CreateAsync(request, EntityId, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.CATEGORY_SLUG_EXISTS", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_Parent_From_Another_Entity()
    {
        var parentId = Guid.CreateVersion7();
        _repo.Setup(r => r.GetByIdAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KbCategory { Id = parentId, EntityId = Guid.CreateVersion7() });

        var request = new CreateKbCategoryRequest(parentId, "Child", null, null);
        var result = await CreateSut().CreateAsync(request, EntityId, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.CATEGORY_PARENT_CROSS_ENTITY", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_Rejects_Making_A_Category_Its_Own_Parent()
    {
        var category = Category();
        _repo.Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);

        var request = new UpdateKbCategoryRequest(category.Id, "Network", null, null, true);
        var result = await CreateSut().UpdateAsync(category.Id, request, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.CATEGORY_CYCLE", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_Rejects_Moving_A_Category_Under_Its_Own_Descendant()
    {
        var parent = Category(name: "Parent");
        var descendantId = Guid.CreateVersion7();

        _repo.Setup(r => r.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(parent);
        _repo.Setup(r => r.GetByIdAsync(descendantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KbCategory { Id = descendantId, EntityId = EntityId, ParentId = parent.Id });

        // The prospective parent's ancestor chain contains the category being moved -- so the
        // "new parent" is actually below it, and the move would detach the subtree.
        _repo.Setup(r => r.GetAncestorIdsAsync(descendantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { parent.Id });

        var request = new UpdateKbCategoryRequest(descendantId, "Parent", null, null, true);
        var result = await CreateSut().UpdateAsync(parent.Id, request, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.CATEGORY_CYCLE", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_Allows_A_Legitimate_Reparent()
    {
        var category = Category(name: "Child");
        var newParentId = Guid.CreateVersion7();

        _repo.Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _repo.Setup(r => r.GetByIdAsync(newParentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KbCategory { Id = newParentId, EntityId = EntityId });
        _repo.Setup(r => r.GetAncestorIdsAsync(newParentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        _repo.Setup(r => r.SlugExistsAsync("child", EntityId, category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repo.Setup(r => r.Update(category));
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.CountArticlesAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var request = new UpdateKbCategoryRequest(newParentId, "Child", null, null, true);
        var result = await CreateSut().UpdateAsync(category.Id, request, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(newParentId, result.Value.ParentId);
    }

    [Fact]
    public async Task DeleteAsync_Refuses_While_Child_Categories_Exist()
    {
        var category = Category();
        _repo.Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _repo.Setup(r => r.HasChildrenAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().DeleteAsync(category.Id, ActorId);

        // Cascading a soft delete through a subtree the caller can't see is worse than refusing.
        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.CATEGORY_HAS_CHILDREN", result.Error!.Code);
    }

    [Fact]
    public async Task DeleteAsync_Refuses_While_Articles_Remain()
    {
        var category = Category();
        _repo.Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _repo.Setup(r => r.HasChildrenAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repo.Setup(r => r.CountArticlesAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(4);

        var result = await CreateSut().DeleteAsync(category.Id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.CATEGORY_HAS_ARTICLES", result.Error!.Code);
        Assert.Contains("4", result.Error.Message);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_An_Empty_Category()
    {
        var category = Category();
        _repo.Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        _repo.Setup(r => r.HasChildrenAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repo.Setup(r => r.CountArticlesAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _repo.Setup(r => r.Update(category));
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().DeleteAsync(category.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(category.IsDeleted);
        Assert.NotNull(category.DeletedAt);
    }

    [Fact]
    public async Task GetTreeAsync_Nests_Children_Under_Their_Parents()
    {
        var root = Category(name: "Root");
        var childA = Category(parentId: root.Id, name: "Child A");
        var childB = Category(parentId: root.Id, name: "Child B");
        var grandchild = Category(parentId: childA.Id, name: "Grandchild");
        var otherRoot = Category(name: "Other Root");

        _repo.Setup(r => r.GetAllAsync(EntityId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { root, childA, childB, grandchild, otherRoot });

        var result = await CreateSut().GetTreeAsync(EntityId, includeInactive: false);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count); // two roots

        var rootNode = result.Value.Single(n => n.Id == root.Id);
        Assert.Equal(2, rootNode.Children.Count);

        var childANode = rootNode.Children.Single(n => n.Id == childA.Id);
        Assert.Equal(grandchild.Id, childANode.Children.Single().Id);

        Assert.Empty(result.Value.Single(n => n.Id == otherRoot.Id).Children);
    }

    [Fact]
    public async Task GetTreeAsync_Survives_A_Cyclic_Parent_Chain_In_Existing_Data()
    {
        // Two categories pointing at each other -- impossible via the service, but reachable by
        // direct SQL. Building the tree must terminate rather than recurse forever.
        var a = Category(name: "A");
        var b = Category(parentId: a.Id, name: "B");
        a.ParentId = b.Id;

        _repo.Setup(r => r.GetAllAsync(EntityId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { a, b });

        var result = await CreateSut().GetTreeAsync(EntityId, includeInactive: false);

        Assert.True(result.IsSuccess);
        // Neither node is a root, so the tree comes back empty instead of hanging.
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GetByIdAsync_Returns_NotFound_For_A_Missing_Category()
    {
        var missingId = Guid.CreateVersion7();
        _repo.Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KbCategory?)null);

        var result = await CreateSut().GetByIdAsync(missingId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.CATEGORY_NOT_FOUND", result.Error!.Code);
    }
}
