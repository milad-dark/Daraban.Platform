using Daraban.Modules.Financial.Data.Entities;

namespace Daraban.Modules.Financial.Data.Repositories;

public interface IInfocomRepository
{
    Task<Infocom?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Infocom?> GetByAssetIdAsync(Guid assetId, CancellationToken ct = default);
    Task<(IReadOnlyList<Infocom> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        Guid? supplierId,
        Guid? budgetId,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task AddAsync(Infocom infocom, CancellationToken ct = default);
    Task UpdateAsync(Infocom infocom, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> AssetHasInfocomAsync(Guid assetId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
