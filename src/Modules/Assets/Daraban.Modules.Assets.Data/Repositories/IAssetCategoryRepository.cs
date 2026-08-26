using Daraban.Modules.Assets.Data.Entities;
namespace Daraban.Modules.Assets.Data.Repositories;

public interface IAssetCategoryRepository
{
    Task<IReadOnlyList<AssetCategory>> GetAllAsync(CancellationToken ct = default);
    Task<AssetCategory?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(AssetCategory category, CancellationToken ct = default);
    Task UpdateAsync(AssetCategory category, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
