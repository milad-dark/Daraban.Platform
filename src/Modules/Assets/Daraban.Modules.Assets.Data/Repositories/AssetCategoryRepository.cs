using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Assets.Data.Repositories;

public class AssetCategoryRepository : IAssetCategoryRepository
{
    private readonly AssetsDbContext _db;
    public AssetCategoryRepository(AssetsDbContext db) => _db = db;

    public async Task<IReadOnlyList<AssetCategory>> GetAllAsync(CancellationToken ct = default)
        => (await _db.AssetCategories
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct)).AsReadOnly();

    public Task<AssetCategory?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.AssetCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddAsync(AssetCategory category, CancellationToken ct = default)
        => await _db.AssetCategories.AddAsync(category, ct);

    public Task UpdateAsync(AssetCategory category, CancellationToken ct = default)
    {
        _db.AssetCategories.Update(category);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public Task<bool> HasChildrenAsync(Guid id, CancellationToken ct = default)
        // The query filter on DeletedAt already hides soft-deleted children, so a deleted child
        // does not block deleting its parent.
        => _db.AssetCategories.AnyAsync(c => c.ParentId == id, ct);

    public Task<bool> HasAssetTypesAsync(Guid id, CancellationToken ct = default)
        => _db.AssetTypes.AnyAsync(t => t.CategoryId == id, ct);
}
