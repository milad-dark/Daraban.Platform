using Daraban.Modules.Assets.Data.Entities;
namespace Daraban.Modules.Assets.Data.Repositories;

public interface IManufacturerRepository
{
    Task<IReadOnlyList<Manufacturer>> GetAllAsync(CancellationToken ct = default);
    Task<Manufacturer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Manufacturer manufacturer, CancellationToken ct = default);
    Task UpdateAsync(Manufacturer manufacturer, CancellationToken ct = default);
}
