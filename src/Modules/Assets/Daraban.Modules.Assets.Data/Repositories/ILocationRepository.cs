using Daraban.Modules.Assets.Data.Entities;
namespace Daraban.Modules.Assets.Data.Repositories;

public interface ILocationRepository
{
    Task<IReadOnlyList<Location>> GetAllAsync(CancellationToken ct = default);
    Task<Location?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Location location, CancellationToken ct = default);
    Task UpdateAsync(Location location, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
