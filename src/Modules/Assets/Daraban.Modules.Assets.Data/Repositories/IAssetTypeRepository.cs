using Daraban.Modules.Assets.Data.Entities;
namespace Daraban.Modules.Assets.Data.Repositories;

public interface IAssetTypeRepository
{
    Task<IReadOnlyList<AssetType>> GetAllAsync(CancellationToken ct = default);
    Task<AssetType?> GetByIdWithFieldsAsync(Guid id, CancellationToken ct = default);
    Task<AssetType?> GetByNameAsync(string name, CancellationToken ct = default);
    Task AddAsync(AssetType assetType, CancellationToken ct = default);
    Task UpdateAsync(AssetType assetType, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
