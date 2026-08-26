using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Assets.Data.Repositories;

public class AssetRepository : IAssetRepository
{
    private readonly AssetsDbContext _db;
    public AssetRepository(AssetsDbContext db) => _db = db;

    public Task<Asset?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Assets
            .Include(a => a.AssetType)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<(IReadOnlyList<Asset> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        AssetStatus? status,
        Guid? assetTypeId,
        Guid? locationId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.Assets
            .Where(a => a.EntityNodeId == entityNodeId);

        if (status is not null)
            query = query.Where(a => a.Status == status);

        if (assetTypeId is not null)
            query = query.Where(a => a.AssetTypeId == assetTypeId);

        if (locationId is not null)
            query = query.Where(a => a.LocationId == locationId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a =>
                a.Name.Contains(search) ||
                (a.AssetTag != null && a.AssetTag.Contains(search)) ||
                (a.SerialNumber != null && a.SerialNumber.Contains(search)));

        var total = await query.CountAsync(ct);
        var items = (await query
            .Include(a => a.AssetType)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)).AsReadOnly();

        return (items, total);
    }

    public async Task AddAsync(Asset asset, CancellationToken ct = default)
        => await _db.Assets.AddAsync(asset, ct);

    public Task UpdateAsync(Asset asset, CancellationToken ct = default)
    {
        _db.Assets.Update(asset);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _db.Assets.AnyAsync(a => a.Id == id, ct);

    public Task<bool> AssetTagExistsAsync(string assetTag, Guid? excludeId, CancellationToken ct = default)
        => _db.Assets.AnyAsync(a => a.AssetTag == assetTag && (excludeId == null || a.Id != excludeId), ct);

    public Task<bool> SerialNumberExistsAsync(string serialNumber, Guid? excludeId, CancellationToken ct = default)
        => _db.Assets.AnyAsync(a => a.SerialNumber == serialNumber && (excludeId == null || a.Id != excludeId), ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
