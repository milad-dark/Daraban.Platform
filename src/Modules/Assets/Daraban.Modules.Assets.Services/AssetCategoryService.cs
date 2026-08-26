using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services;

public class AssetCategoryService : IAssetCategoryService
{
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
            return Result.Failure<AssetCategoryDto>(new Error("ASSETS.CATEGORY_NOT_FOUND", "Asset category not found.", ErrorType.NotFound));

        return Result.Success(new AssetCategoryDto(
            category.Id, category.ParentId, category.Name, category.Description, category.SortOrder));
    }

    public async Task<Result<AssetCategoryDto>> CreateAsync(CreateAssetCategoryRequest request, CancellationToken ct = default)
    {
        if (request.ParentId is not null)
        {
            var parent = await _repository.GetByIdAsync(request.ParentId.Value, ct);
            if (parent is null)
                return Result.Failure<AssetCategoryDto>(new Error("ASSETS.CATEGORY_NOT_FOUND", "Parent category not found.", ErrorType.NotFound));
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
            return Result.Failure<AssetCategoryDto>(new Error("ASSETS.CATEGORY_NOT_FOUND", "Asset category not found.", ErrorType.NotFound));

        if (request.ParentId is not null && request.ParentId != id)
        {
            var parent = await _repository.GetByIdAsync(request.ParentId.Value, ct);
            if (parent is null)
                return Result.Failure<AssetCategoryDto>(new Error("ASSETS.CATEGORY_NOT_FOUND", "Parent category not found.", ErrorType.NotFound));
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
            return Result.Failure(new Error("ASSETS.CATEGORY_NOT_FOUND", "Asset category not found.", ErrorType.NotFound));

        category.DeletedAt = DateTimeOffset.UtcNow;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
