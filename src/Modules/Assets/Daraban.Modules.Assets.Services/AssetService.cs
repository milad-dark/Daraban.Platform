using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services;

public class AssetService : IAssetService
{
    private readonly IAssetRepository _assetRepository;
    private readonly IAssetTypeRepository _assetTypeRepository;

    public AssetService(IAssetRepository assetRepository, IAssetTypeRepository assetTypeRepository)
    {
        _assetRepository = assetRepository;
        _assetTypeRepository = assetTypeRepository;
    }

    public async Task<Result<AssetPagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? status,
        Guid? assetTypeId,
        Guid? locationId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        AssetStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AssetStatus>(status, true, out var s))
            parsedStatus = s;

        var (items, total) = await _assetRepository.GetPagedAsync(
            entityNodeId, parsedStatus, assetTypeId, locationId, search, page, pageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result.Success(new AssetPagedResult(dtos, total, page, pageSize));
    }

    public async Task<Result<AssetDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var asset = await _assetRepository.GetByIdAsync(id, ct);
        if (asset is null)
            return Result.Failure<AssetDto>(new Error("ASSETS.ASSET_NOT_FOUND", "Asset not found.", ErrorType.NotFound));

        return Result.Success(MapToDto(asset));
    }

    public async Task<Result<AssetDto>> CreateAsync(CreateAssetRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        // Validate AssetType exists
        var assetType = await _assetTypeRepository.GetByIdWithFieldsAsync(request.AssetTypeId, ct);
        if (assetType is null)
            return Result.Failure<AssetDto>(new Error("ASSETS.ASSET_TYPE_NOT_FOUND", "Asset type not found.", ErrorType.NotFound));

        // Validate unique AssetTag
        if (!string.IsNullOrWhiteSpace(request.AssetTag))
        {
            var tagExists = await _assetRepository.AssetTagExistsAsync(request.AssetTag, null, ct);
            if (tagExists)
                return Result.Failure<AssetDto>(new Error("ASSETS.ASSET_TAG_DUPLICATE", "An asset with this tag already exists.", ErrorType.Conflict));
        }

        // Validate unique SerialNumber
        if (!string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            var serialExists = await _assetRepository.SerialNumberExistsAsync(request.SerialNumber, null, ct);
            if (serialExists)
                return Result.Failure<AssetDto>(new Error("ASSETS.SERIAL_NUMBER_DUPLICATE", "An asset with this serial number already exists.", ErrorType.Conflict));
        }

        var now = DateTimeOffset.UtcNow;
        var asset = new Asset
        {
            Id = Guid.CreateVersion7(),
            AssetTypeId = request.AssetTypeId,
            AssetModelId = request.AssetModelId,
            LocationId = request.LocationId,
            EntityNodeId = request.EntityNodeId,
            Name = request.Name,
            AssetTag = request.AssetTag,
            SerialNumber = request.SerialNumber,
            Status = AssetStatus.InStock,
            PurchaseDate = request.PurchaseDate,
            PurchaseCost = request.PurchaseCost,
            PurchaseCurrency = request.PurchaseCurrency,
            OrderNumber = request.OrderNumber,
            SupplierName = request.SupplierName,
            WarrantyExpiry = request.WarrantyExpiry,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _assetRepository.AddAsync(asset, ct);
        await _assetRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(asset));
    }

    public async Task<Result<AssetDto>> UpdateAsync(Guid id, UpdateAssetRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var asset = await _assetRepository.GetByIdAsync(id, ct);
        if (asset is null)
            return Result.Failure<AssetDto>(new Error("ASSETS.ASSET_NOT_FOUND", "Asset not found.", ErrorType.NotFound));

        // Validate unique AssetTag (excluding current asset)
        if (!string.IsNullOrWhiteSpace(request.AssetTag))
        {
            var tagExists = await _assetRepository.AssetTagExistsAsync(request.AssetTag, id, ct);
            if (tagExists)
                return Result.Failure<AssetDto>(new Error("ASSETS.ASSET_TAG_DUPLICATE", "An asset with this tag already exists.", ErrorType.Conflict));
        }

        // Validate unique SerialNumber (excluding current asset)
        if (!string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            var serialExists = await _assetRepository.SerialNumberExistsAsync(request.SerialNumber, id, ct);
            if (serialExists)
                return Result.Failure<AssetDto>(new Error("ASSETS.SERIAL_NUMBER_DUPLICATE", "An asset with this serial number already exists.", ErrorType.Conflict));
        }

        asset.Name = request.Name;
        asset.AssetModelId = request.AssetModelId;
        asset.LocationId = request.LocationId;
        asset.AssetTag = request.AssetTag;
        asset.SerialNumber = request.SerialNumber;
        asset.PurchaseDate = request.PurchaseDate;
        asset.PurchaseCost = request.PurchaseCost;
        asset.PurchaseCurrency = request.PurchaseCurrency;
        asset.OrderNumber = request.OrderNumber;
        asset.SupplierName = request.SupplierName;
        asset.WarrantyExpiry = request.WarrantyExpiry;
        asset.Notes = request.Notes;
        asset.UpdatedAt = DateTimeOffset.UtcNow;

        await _assetRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(asset));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var asset = await _assetRepository.GetByIdAsync(id, ct);
        if (asset is null)
            return Result.Failure(new Error("ASSETS.ASSET_NOT_FOUND", "Asset not found.", ErrorType.NotFound));

        asset.DeletedAt = DateTimeOffset.UtcNow;
        asset.UpdatedAt = DateTimeOffset.UtcNow;
        await _assetRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static AssetDto MapToDto(Asset a) => new(
        a.Id,
        a.Name,
        a.AssetTag,
        a.SerialNumber,
        a.Status,
        a.AssetType?.Name ?? string.Empty,
        null, // AssetModel name — populated by caller if needed
        null, // Manufacturer name — populated by caller if needed
        null, // Location name — populated by caller if needed
        a.PurchaseDate,
        a.PurchaseCost,
        a.PurchaseCurrency,
        a.WarrantyExpiry,
        a.Notes,
        a.CreatedAt,
        a.UpdatedAt);

    private static AssetListDto MapToListDto(Asset a) => new(
        a.Id,
        a.Name,
        a.AssetTag,
        a.SerialNumber,
        a.Status,
        a.AssetType?.Name ?? string.Empty,
        null, // Location name
        a.WarrantyExpiry);
}
