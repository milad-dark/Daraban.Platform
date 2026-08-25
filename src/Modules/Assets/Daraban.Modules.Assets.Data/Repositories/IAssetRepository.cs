using Daraban.Modules.Assets.Data.Entities;
namespace Daraban.Modules.Assets.Data.Repositories;

public interface IAssetRepository
{
    Task<Asset?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Asset> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        AssetStatus? status,
        Guid? assetTypeId,
        Guid? locationId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task AddAsync(Asset asset, CancellationToken ct = default);
    Task UpdateAsync(Asset asset, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> AssetTagExistsAsync(string assetTag, Guid? excludeId, CancellationToken ct = default);
    Task<bool> SerialNumberExistsAsync(string serialNumber, Guid? excludeId, CancellationToken ct = default);
}
