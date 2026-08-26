using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Assets.Data.Repositories;

public class ManufacturerRepository : IManufacturerRepository
{
    private readonly AssetsDbContext _db;
    public ManufacturerRepository(AssetsDbContext db) => _db = db;

    public async Task<IReadOnlyList<Manufacturer>> GetAllAsync(CancellationToken ct = default)
        => (await _db.Manufacturers
            .OrderBy(m => m.Name)
            .ToListAsync(ct)).AsReadOnly();

    public Task<Manufacturer?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Manufacturers.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId, CancellationToken ct = default)
        => _db.Manufacturers.AnyAsync(m => m.Name == name && (excludeId == null || m.Id != excludeId), ct);

    public async Task AddAsync(Manufacturer manufacturer, CancellationToken ct = default)
        => await _db.Manufacturers.AddAsync(manufacturer, ct);

    public Task UpdateAsync(Manufacturer manufacturer, CancellationToken ct = default)
    {
        _db.Manufacturers.Update(manufacturer);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
