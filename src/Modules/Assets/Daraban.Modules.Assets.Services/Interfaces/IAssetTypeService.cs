using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services.Interfaces;

public interface IAssetTypeService
{
    Task<Result<IReadOnlyList<AssetTypeDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<AssetTypeDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<AssetTypeDto>> CreateAsync(CreateAssetTypeRequest request, CancellationToken ct = default);
    Task<Result<AssetTypeDto>> UpdateAsync(Guid id, CreateAssetTypeRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
