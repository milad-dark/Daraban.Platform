using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Assets.Data.Repositories;

public class LocationRepository : ILocationRepository
{
    private readonly AssetsDbContext _db;
    public LocationRepository(AssetsDbContext db) => _db = db;

    public async Task<IReadOnlyList<Location>> GetAllAsync(CancellationToken ct = default)
        => (await _db.Locations
            .OrderBy(l => l.Name)
            .ToListAsync(ct)).AsReadOnly();

    public Task<Location?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task AddAsync(Location location, CancellationToken ct = default)
        => await _db.Locations.AddAsync(location, ct);

    public Task UpdateAsync(Location location, CancellationToken ct = default)
    {
        _db.Locations.Update(location);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public Task<bool> HasChildrenAsync(Guid id, CancellationToken ct = default)
        // The query filter on DeletedAt already hides soft-deleted children.
        => _db.Locations.AnyAsync(l => l.ParentId == id, ct);

    public Task<bool> HasAssetsAsync(Guid id, CancellationToken ct = default)
        // Assets has a soft-delete filter on DeletedAt too, so retired assets don't block.
        => _db.Assets.AnyAsync(a => a.LocationId == id, ct);
}
