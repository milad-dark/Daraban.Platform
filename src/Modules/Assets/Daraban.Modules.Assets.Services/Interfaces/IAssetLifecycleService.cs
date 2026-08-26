using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services.Interfaces;

public interface IAssetLifecycleService
{
    Task<Result<AssetStatusHistoryDto>> TransitionAsync(Guid assetId, LifecycleTransitionRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AssetStatusHistoryDto>>> GetHistoryAsync(Guid assetId, CancellationToken ct = default);
}
