using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services;

public class AssetCategoryService : IAssetCategoryService
{
    /// <summary>Hard ceiling on the ancestor walk -- a corrupted parent chain must not spin forever.</summary>
    private const int MaxTreeDepth = 32;

    private readonly IAssetCategoryRepository _repository;

    public AssetCategoryService(IAssetCategoryRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<AssetCategoryDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _repository.GetAllAsync(ct);
        var dtos = categories.Select(c => new AssetCategoryDto(
            c.Id, c.ParentId, c.Name, c.Description, c.SortOrder)).ToList();
        return Result.Success<IReadOnlyList<AssetCategoryDto>>(dtos);
    }

    public async Task<Result<AssetCategoryDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _repository.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure<AssetCategoryDto>(NotFound());

        return Result.Success(new AssetCategoryDto(
            category.Id, category.ParentId, category.Name, category.Description, category.SortOrder));
    }

    public async Task<Result<AssetCategoryDto>> CreateAsync(CreateAssetCategoryRequest request, CancellationToken ct = default)
    {
        if (request.ParentId is not null)
        {
            var parent = await _repository.GetByIdAsync(request.ParentId.Value, ct);
            if (parent is null)
                return Result.Failure<AssetCategoryDto>(ParentNotFound());
        }

        var now = DateTimeOffset.UtcNow;
        var category = new AssetCategory
        {
            Id = Guid.CreateVersion7(),
            ParentId = request.ParentId,
            Name = request.Name,
            Description = request.Description,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _repository.AddAsync(category, ct);
        await _repository.SaveChangesAsync(ct);

        return Result.Success(new AssetCategoryDto(
            category.Id, category.ParentId, category.Name, category.Description, category.SortOrder));
    }

    public async Task<Result<AssetCategoryDto>> UpdateAsync(Guid id, CreateAssetCategoryRequest request, CancellationToken ct = default)
    {
        var category = await _repository.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure<AssetCategoryDto>(NotFound());

        if (request.ParentId != category.ParentId && request.ParentId is not null)
        {
            if (request.ParentId.Value == id)
                return Result.Failure<AssetCategoryDto>(Cycle());

            var parent = await _repository.GetByIdAsync(request.ParentId.Value, ct);
            if (parent is null)
                return Result.Failure<AssetCategoryDto>(ParentNotFound());

            // Re-parenting under one's own descendant would detach the subtree from the root and
            // create a cycle. The whole category list is already loaded cheaply (small taxonomy),
            // so walking it in memory is simpler than a recursive CTE and needs no extra repo surface.
            var all = await _repository.GetAllAsync(ct);
            if (WouldCreateCycle(all, id, request.ParentId.Value))
                return Result.Failure<AssetCategoryDto>(Cycle());
        }

        category.ParentId = request.ParentId;
        category.Name = request.Name;
        category.Description = request.Description;
        category.SortOrder = request.SortOrder;
        category.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.SaveChangesAsync(ct);

        return Result.Success(new AssetCategoryDto(
            category.Id, category.ParentId, category.Name, category.Description, category.SortOrder));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _repository.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure(NotFound());

        // Soft-deleting a parent without checking orphans the children: their ParentId keeps
        // pointing at a row the query filter hides, so they silently vanish from every subtree
        // walk that resolves names through the parent.
        if (await _repository.HasChildrenAsync(id, ct))
            return Result.Failure(new Error(
                "ASSETS.CATEGORY_HAS_CHILDREN",
                "Cannot delete a category that still has child categories.", ErrorType.BusinessRule));

        if (await _repository.HasAssetTypesAsync(id, ct))
            return Result.Failure(new Error(
                "ASSETS.CATEGORY_HAS_TYPES",
                "Cannot delete a category that still has asset types filed under it.", ErrorType.BusinessRule));

        var now = DateTimeOffset.UtcNow;
        category.DeletedAt = now;
        category.UpdatedAt = now;
        await _repository.SaveChangesAsync(ct);

        return Result.Success();
    }

    // ---- helpers ---------------------------------------------------------------------------

    /// <summary>
    /// Walks up from the prospective new parent: if the category being moved appears among its
    /// ancestors, the "new parent" is actually below us and the move would create a cycle.
    /// </summary>
    private static bool WouldCreateCycle(IReadOnlyList<AssetCategory> all, Guid movedId, Guid newParentId)
    {
        var byId = all.ToDictionary(c => c.Id);
        var seen = new HashSet<Guid>();
        var currentId = newParentId;

        for (var depth = 0; depth < MaxTreeDepth && byId.TryGetValue(currentId, out var current); depth++)
        {
            if (current.Id == movedId)
                return true;

            // Guard against a pre-existing cycle in the stored data: stop instead of looping.
            if (!seen.Add(current.Id))
                return true;

            if (current.ParentId is null)
                return false;

            currentId = current.ParentId.Value;
        }

        return false;
    }

    private static Error NotFound()
        => new("ASSETS.CATEGORY_NOT_FOUND", "Asset category not found.", ErrorType.NotFound);

    private static Error ParentNotFound()
        => new("ASSETS.CATEGORY_NOT_FOUND", "Parent category not found.", ErrorType.NotFound);

    private static Error Cycle()
        => new("ASSETS.CATEGORY_CYCLE",
            "Cannot move a category beneath itself or one of its own descendants.", ErrorType.BusinessRule);
}
