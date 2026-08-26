using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Assets.Data.Repositories;

public class AssetStatusHistoryRepository : IAssetStatusHistoryRepository
{
    private readonly AssetsDbContext _db;
    public AssetStatusHistoryRepository(AssetsDbContext db) => _db = db;

    public async Task<IReadOnlyList<AssetStatusHistory>> GetByAssetIdAsync(Guid assetId, CancellationToken ct = default)
        => (await _db.AssetStatusHistories
            .Where(h => h.AssetId == assetId)
            .OrderByDescending(h => h.OccurredAt)
            .ToListAsync(ct)).AsReadOnly();

    public async Task AddAsync(AssetStatusHistory history, CancellationToken ct = default)
        => await _db.AssetStatusHistories.AddAsync(history, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
