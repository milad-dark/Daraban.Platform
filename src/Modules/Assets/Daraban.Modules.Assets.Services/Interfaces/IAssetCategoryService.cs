using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services.Interfaces;

public interface IAssetCategoryService
{
    Task<Result<IReadOnlyList<AssetCategoryDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<AssetCategoryDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<AssetCategoryDto>> CreateAsync(CreateAssetCategoryRequest request, CancellationToken ct = default);
    Task<Result<AssetCategoryDto>> UpdateAsync(Guid id, CreateAssetCategoryRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
