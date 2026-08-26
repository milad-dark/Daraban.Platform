using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services.Interfaces;

public interface IAssetService
{
    Task<Result<AssetPagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? status,
        Guid? assetTypeId,
        Guid? locationId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<Result<AssetDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<AssetDto>> CreateAsync(CreateAssetRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result<AssetDto>> UpdateAsync(Guid id, UpdateAssetRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
}
