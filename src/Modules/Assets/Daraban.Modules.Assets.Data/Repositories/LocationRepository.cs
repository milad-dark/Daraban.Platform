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
}
