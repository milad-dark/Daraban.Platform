using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Assets.Data.Repositories;

public class AssetTypeRepository : IAssetTypeRepository
{
    private readonly AssetsDbContext _db;
    public AssetTypeRepository(AssetsDbContext db) => _db = db;

    public async Task<IReadOnlyList<AssetType>> GetAllAsync(CancellationToken ct = default)
        => (await _db.AssetTypes
            .Include(t => t.Category)
            .OrderBy(t => t.Name)
            .ToListAsync(ct)).AsReadOnly();

    public Task<AssetType?> GetByIdWithFieldsAsync(Guid id, CancellationToken ct = default)
        => _db.AssetTypes
            .Include(t => t.Category)
            .Include(t => t.Fields)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddAsync(AssetType assetType, CancellationToken ct = default)
        => await _db.AssetTypes.AddAsync(assetType, ct);

    public Task UpdateAsync(AssetType assetType, CancellationToken ct = default)
    {
        _db.AssetTypes.Update(assetType);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
