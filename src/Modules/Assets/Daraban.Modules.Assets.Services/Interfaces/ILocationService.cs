using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services.Interfaces;

public interface ILocationService
{
    Task<Result<IReadOnlyList<LocationDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<LocationDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<LocationDto>> CreateAsync(CreateLocationRequest request, CancellationToken ct = default);
    Task<Result<LocationDto>> UpdateAsync(Guid id, CreateLocationRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
