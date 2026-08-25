using Daraban.Modules.Assets.Data.Entities;
namespace Daraban.Modules.Assets.Data.Repositories;

public interface IAssetStatusHistoryRepository
{
    Task<IReadOnlyList<AssetStatusHistory>> GetByAssetIdAsync(Guid assetId, CancellationToken ct = default);
    Task AddAsync(AssetStatusHistory history, CancellationToken ct = default);
}
