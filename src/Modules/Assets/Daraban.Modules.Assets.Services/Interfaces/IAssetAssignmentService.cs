using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services.Interfaces;

public interface IAssetAssignmentService
{
    Task<Result<AssetAssignmentDto>> AssignAsync(Guid assetId, AssignAssetRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> UnassignAsync(Guid assetId, Guid actorUserId, string? notes, CancellationToken ct = default);
    Task<Result<AssetAssignmentDto?>> GetCurrentAsync(Guid assetId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AssetAssignmentDto>>> GetHistoryAsync(Guid assetId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AssetListDto>>> GetByTargetAsync(AssignmentTargetType targetType, Guid targetId, CancellationToken ct = default);
}
