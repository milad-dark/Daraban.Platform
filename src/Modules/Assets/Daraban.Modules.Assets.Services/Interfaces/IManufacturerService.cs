using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services.Interfaces;

public interface IManufacturerService
{
    Task<Result<IReadOnlyList<ManufacturerDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<ManufacturerDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<ManufacturerDto>> CreateAsync(CreateManufacturerRequest request, CancellationToken ct = default);
    Task<Result<ManufacturerDto>> UpdateAsync(Guid id, CreateManufacturerRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
