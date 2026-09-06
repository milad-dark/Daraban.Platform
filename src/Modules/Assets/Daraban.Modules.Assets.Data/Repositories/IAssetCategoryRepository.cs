using Daraban.Modules.Assets.Data.Entities;
namespace Daraban.Modules.Assets.Data.Repositories;

public interface IAssetCategoryRepository
{
    Task<IReadOnlyList<AssetCategory>> GetAllAsync(CancellationToken ct = default);
    Task<AssetCategory?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(AssetCategory category, CancellationToken ct = default);
    Task UpdateAsync(AssetCategory category, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Whether any category lists <paramref name="id"/> as its parent.</summary>
    Task<bool> HasChildrenAsync(Guid id, CancellationToken ct = default);

    /// <summary>Whether any asset type is still filed under this category.</summary>
    Task<bool> HasAssetTypesAsync(Guid id, CancellationToken ct = default);
}
