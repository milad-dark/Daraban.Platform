using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services;

public class AssetTypeService : IAssetTypeService
{
    private readonly IAssetTypeRepository _repository;
    private readonly IAssetCategoryRepository _categoryRepository;

    public AssetTypeService(IAssetTypeRepository repository, IAssetCategoryRepository categoryRepository)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
    }

    public async Task<Result<IReadOnlyList<AssetTypeDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var types = await _repository.GetAllAsync(ct);
        var dtos = types.Select(t => new AssetTypeDto(
            t.Id, t.CategoryId, t.Category?.Name ?? string.Empty,
            t.Name, t.Description, t.Icon)).ToList();
        return Result.Success<IReadOnlyList<AssetTypeDto>>(dtos);
    }

    public async Task<Result<AssetTypeDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var type = await _repository.GetByIdWithFieldsAsync(id, ct);
        if (type is null)
            return Result.Failure<AssetTypeDto>(new Error("ASSETS.ASSET_TYPE_NOT_FOUND", "Asset type not found.", ErrorType.NotFound));

        return Result.Success(new AssetTypeDto(
            type.Id, type.CategoryId, type.Category?.Name ?? string.Empty,
            type.Name, type.Description, type.Icon));
    }

    public async Task<Result<AssetTypeDto>> CreateAsync(CreateAssetTypeRequest request, CancellationToken ct = default)
    {
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, ct);
        if (category is null)
            return Result.Failure<AssetTypeDto>(new Error("ASSETS.CATEGORY_NOT_FOUND", "Asset category not found.", ErrorType.NotFound));

        var now = DateTimeOffset.UtcNow;
        var assetType = new AssetType
        {
            Id = Guid.CreateVersion7(),
            CategoryId = request.CategoryId,
            Name = request.Name,
            Description = request.Description,
            Icon = request.Icon,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _repository.AddAsync(assetType, ct);
        await _repository.SaveChangesAsync(ct);

        return Result.Success(new AssetTypeDto(
            assetType.Id, assetType.CategoryId, category.Name,
            assetType.Name, assetType.Description, assetType.Icon));
    }

    public async Task<Result<AssetTypeDto>> UpdateAsync(Guid id, CreateAssetTypeRequest request, CancellationToken ct = default)
    {
        var assetType = await _repository.GetByIdWithFieldsAsync(id, ct);
        if (assetType is null)
            return Result.Failure<AssetTypeDto>(new Error("ASSETS.ASSET_TYPE_NOT_FOUND", "Asset type not found.", ErrorType.NotFound));

        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, ct);
        if (category is null)
            return Result.Failure<AssetTypeDto>(new Error("ASSETS.CATEGORY_NOT_FOUND", "Asset category not found.", ErrorType.NotFound));

        assetType.CategoryId = request.CategoryId;
        assetType.Name = request.Name;
        assetType.Description = request.Description;
        assetType.Icon = request.Icon;
        assetType.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.SaveChangesAsync(ct);

        return Result.Success(new AssetTypeDto(
            assetType.Id, assetType.CategoryId, category.Name,
            assetType.Name, assetType.Description, assetType.Icon));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var assetType = await _repository.GetByIdWithFieldsAsync(id, ct);
        if (assetType is null)
            return Result.Failure(new Error("ASSETS.ASSET_TYPE_NOT_FOUND", "Asset type not found.", ErrorType.NotFound));

        assetType.DeletedAt = DateTimeOffset.UtcNow;
        assetType.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
