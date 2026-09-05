using Daraban.Modules.Knowledge.Data.Entities;
using Daraban.Modules.Knowledge.Data.Repositories;
using Daraban.Modules.Knowledge.Services.Dtos;
using Daraban.Modules.Knowledge.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.Knowledge.Services;

public class KbCategoryService : IKbCategoryService
{
    private readonly IKbCategoryRepository _categories;

    public KbCategoryService(IKbCategoryRepository categories) => _categories = categories;

    public async Task<Result<IReadOnlyList<KbCategoryDto>>> GetAllAsync(
        Guid entityNodeId, bool includeInactive, CancellationToken ct = default)
    {
        var categories = await _categories.GetAllAsync(entityNodeId, includeInactive, ct);

        // ArticleCount is reported as 0 on the flat list: filling it would mean one COUNT per
        // category (an N+1). The detail endpoint returns the real count for a single category.
        var dtos = categories.Select(c => MapToDto(c, articleCount: 0)).ToList();
        return Result.Success<IReadOnlyList<KbCategoryDto>>(dtos);
    }

    public async Task<Result<IReadOnlyList<KbCategoryTreeDto>>> GetTreeAsync(
        Guid entityNodeId, bool includeInactive, CancellationToken ct = default)
    {
        var flat = await _categories.GetAllAsync(entityNodeId, includeInactive, ct);

        // One flat query, tree assembled in memory. Children are grouped by ParentId first so
        // this is O(n), not O(n^2) from re-scanning the list at every level.
        var byParent = flat
            .Where(c => c.ParentId.HasValue)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var roots = flat.Where(c => c.ParentId is null).ToList();
        var tree = roots.Select(r => BuildNode(r, byParent, depth: 0)).ToList();

        return Result.Success<IReadOnlyList<KbCategoryTreeDto>>(tree);
    }

    public async Task<Result<KbCategoryDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _categories.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure<KbCategoryDto>(NotFound());

        var articleCount = await _categories.CountArticlesAsync(id, ct);
        return Result.Success(MapToDto(category, articleCount));
    }

    public async Task<Result<KbCategoryDto>> CreateAsync(
        CreateKbCategoryRequest request, Guid entityNodeId, Guid actorUserId, CancellationToken ct = default)
    {
        if (request.ParentId is not null)
        {
            var parent = await _categories.GetByIdAsync(request.ParentId.Value, ct);
            if (parent is null)
                return Result.Failure<KbCategoryDto>(new Error(
                    "KNOWLEDGE.CATEGORY_PARENT_NOT_FOUND", "Parent category not found.", ErrorType.NotFound));

            // A category must not be re-homed into another tenant's tree.
            if (parent.EntityId != entityNodeId)
                return Result.Failure<KbCategoryDto>(new Error(
                    "KNOWLEDGE.CATEGORY_PARENT_CROSS_ENTITY",
                    "Parent category belongs to a different entity.", ErrorType.BusinessRule));
        }

        var slug = ResolveSlug(request.Slug, request.Name);
        if (slug.Length == 0)
            return Result.Failure<KbCategoryDto>(new Error(
                "KNOWLEDGE.CATEGORY_SLUG_INVALID",
                "Category name must contain at least one letter or digit.", ErrorType.Validation));

        if (await _categories.SlugExistsAsync(slug, entityNodeId, null, ct))
            return Result.Failure<KbCategoryDto>(SlugExists(slug));

        var now = DateTimeOffset.UtcNow;
        var category = new KbCategory
        {
            Id = Guid.CreateVersion7(),
            EntityId = entityNodeId,
            ParentId = request.ParentId,
            Name = request.Name,
            Slug = slug,
            Description = request.Description,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
        };

        await _categories.AddAsync(category, ct);
        await _categories.SaveChangesAsync(ct);

        return Result.Success(MapToDto(category, articleCount: 0));
    }

    public async Task<Result<KbCategoryDto>> UpdateAsync(
        Guid id, UpdateKbCategoryRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var category = await _categories.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure<KbCategoryDto>(NotFound());

        if (request.ParentId != category.ParentId && request.ParentId is not null)
        {
            if (request.ParentId.Value == id)
                return Result.Failure<KbCategoryDto>(new Error(
                    "KNOWLEDGE.CATEGORY_CYCLE", "A category cannot be its own parent.", ErrorType.BusinessRule));

            var parent = await _categories.GetByIdAsync(request.ParentId.Value, ct);
            if (parent is null)
                return Result.Failure<KbCategoryDto>(new Error(
                    "KNOWLEDGE.CATEGORY_PARENT_NOT_FOUND", "Parent category not found.", ErrorType.NotFound));

            if (parent.EntityId != category.EntityId)
                return Result.Failure<KbCategoryDto>(new Error(
                    "KNOWLEDGE.CATEGORY_PARENT_CROSS_ENTITY",
                    "Parent category belongs to a different entity.", ErrorType.BusinessRule));

            // Re-parenting under one's own descendant would detach the subtree from the root and
            // create a cycle. Walking the prospective parent's ancestors catches that: if this
            // category appears among them, the new parent is below us.
            var ancestorIds = await _categories.GetAncestorIdsAsync(request.ParentId.Value, ct);
            if (ancestorIds.Contains(id))
                return Result.Failure<KbCategoryDto>(new Error(
                    "KNOWLEDGE.CATEGORY_CYCLE",
                    "Cannot move a category beneath one of its own descendants.", ErrorType.BusinessRule));
        }

        var slug = ResolveSlug(request.Slug, request.Name);
        if (slug.Length == 0)
            return Result.Failure<KbCategoryDto>(new Error(
                "KNOWLEDGE.CATEGORY_SLUG_INVALID",
                "Category name must contain at least one letter or digit.", ErrorType.Validation));

        if (await _categories.SlugExistsAsync(slug, category.EntityId, id, ct))
            return Result.Failure<KbCategoryDto>(SlugExists(slug));

        category.ParentId = request.ParentId;
        category.Name = request.Name;
        category.Slug = slug;
        category.Description = request.Description;
        category.IsActive = request.IsActive;
        category.SortOrder = request.SortOrder;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        category.UpdatedById = actorUserId;

        _categories.Update(category);
        await _categories.SaveChangesAsync(ct);

        var articleCount = await _categories.CountArticlesAsync(id, ct);
        return Result.Success(MapToDto(category, articleCount));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var category = await _categories.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure(NotFound());

        // Refuse rather than cascade: silently soft-deleting a subtree, or orphaning articles
        // the caller didn't know about, is worse than making them do it explicitly.
        if (await _categories.HasChildrenAsync(id, ct))
            return Result.Failure(new Error(
                "KNOWLEDGE.CATEGORY_HAS_CHILDREN",
                "Cannot delete a category that still has child categories.", ErrorType.BusinessRule));

        var articleCount = await _categories.CountArticlesAsync(id, ct);
        if (articleCount > 0)
            return Result.Failure(new Error(
                "KNOWLEDGE.CATEGORY_HAS_ARTICLES",
                $"Cannot delete a category that still has {articleCount} article(s).", ErrorType.BusinessRule));

        var now = DateTimeOffset.UtcNow;
        category.IsDeleted = true;
        category.DeletedAt = now;
        category.UpdatedAt = now;
        category.UpdatedById = actorUserId;

        _categories.Update(category);
        await _categories.SaveChangesAsync(ct);

        return Result.Success();
    }

    // ---- helpers -------------------------------------------------------------------------

    /// <summary>Depth guard mirrors KbCategoryRepository.MaxTreeDepth -- a corrupted parent
    /// chain must not recurse without bound while building the tree.</summary>
    private static KbCategoryTreeDto BuildNode(
        KbCategory node, IReadOnlyDictionary<Guid, List<KbCategory>> byParent, int depth)
    {
        var children = depth >= 32 || !byParent.TryGetValue(node.Id, out var kids)
            ? new List<KbCategoryTreeDto>()
            : kids.Select(k => BuildNode(k, byParent, depth + 1)).ToList();

        return new KbCategoryTreeDto(
            node.Id, node.ParentId, node.Name, node.Slug, node.Description,
            node.SortOrder, node.IsActive, children);
    }

    private static string ResolveSlug(string? explicitSlug, string name)
        => KbSlug.From(string.IsNullOrWhiteSpace(explicitSlug) ? name : explicitSlug);

    private static Error NotFound()
        => new("KNOWLEDGE.CATEGORY_NOT_FOUND", "Category not found.", ErrorType.NotFound);

    private static Error SlugExists(string slug)
        => new("KNOWLEDGE.CATEGORY_SLUG_EXISTS", $"A category with slug '{slug}' already exists.", ErrorType.Conflict);

    private static KbCategoryDto MapToDto(KbCategory c, int articleCount) => new(
        c.Id, c.ParentId, c.Name, c.Slug, c.Description,
        c.SortOrder, c.IsActive, articleCount, c.CreatedAt, c.UpdatedAt);
}
