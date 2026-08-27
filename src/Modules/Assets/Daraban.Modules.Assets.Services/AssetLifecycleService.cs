using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Contracts.Assets;

namespace Daraban.Modules.Assets.Services;

public class AssetLifecycleService : IAssetLifecycleService
{
    private readonly IAssetRepository _assetRepository;
    private readonly IAssetStatusHistoryRepository _historyRepository;
    private readonly IAssetAssignmentRepository _assignmentRepository;
    private readonly IEventPublisher _eventPublisher;

    public AssetLifecycleService(
        IAssetRepository assetRepository,
        IAssetStatusHistoryRepository historyRepository,
        IAssetAssignmentRepository assignmentRepository,
        IEventPublisher eventPublisher)
    {
        _assetRepository = assetRepository;
        _historyRepository = historyRepository;
        _assignmentRepository = assignmentRepository;
        _eventPublisher = eventPublisher;
    }

    public async Task<Result<AssetStatusHistoryDto>> TransitionAsync(
        Guid assetId, LifecycleTransitionRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var asset = await _assetRepository.GetByIdAsync(assetId, ct);
        if (asset is null)
            return Result.Failure<AssetStatusHistoryDto>(
                new Error("ASSETS.ASSET_NOT_FOUND", "Asset not found.", ErrorType.NotFound));

        // Validate the requested transition is allowed
        if (!IsTransitionAllowed(asset.Status, request.ToStatus))
            return Result.Failure<AssetStatusHistoryDto>(
                new Error("ASSETS.INVALID_TRANSITION",
                    $"Cannot transition from '{asset.Status}' to '{request.ToStatus}'.",
                    ErrorType.BusinessRule));

        // Retire and Dispose require a reason (roadmap Task 3.4)
        if (request.ToStatus is AssetStatus.Retired or AssetStatus.Disposed
            && string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<AssetStatusHistoryDto>(
                new Error("ASSETS.REASON_REQUIRED",
                    $"A reason is required when transitioning to '{request.ToStatus}'.",
                    ErrorType.BusinessRule));

        var fromStatus = asset.Status;

        // Unassign if retiring or disposing
        if (request.ToStatus is AssetStatus.Retired or AssetStatus.Disposed)
        {
            var current = await _assignmentRepository.GetCurrentAsync(assetId, ct);
            if (current is not null)
            {
                current.IsCurrent = false;
                current.UnassignedAt = DateTimeOffset.UtcNow;
                await _assignmentRepository.UpdateAsync(current, ct);
            }
        }

        // Apply transition
        asset.Status = request.ToStatus;
        asset.UpdatedAt = DateTimeOffset.UtcNow;

        // Record history
        var now = DateTimeOffset.UtcNow;
        var history = new AssetStatusHistory
        {
            Id = Guid.CreateVersion7(),
            AssetId = assetId,
            FromStatus = fromStatus,
            ToStatus = request.ToStatus,
            ActorUserId = actorUserId,
            Reason = request.Reason,
            Notes = request.Notes,
            OccurredAt = now,
        };

        await _historyRepository.AddAsync(history, ct);
        await _assetRepository.SaveChangesAsync(ct);

        // Publish event (fire-and-forget is acceptable here — the DB write is the source of truth)
        await _eventPublisher.PublishAsync(new AssetLifecycleChangedEvent(
            assetId, asset.EntityNodeId,
            fromStatus.ToString(), request.ToStatus.ToString(),
            actorUserId, request.Reason), ct);

        return Result.Success(new AssetStatusHistoryDto(
            history.Id, history.FromStatus, history.ToStatus,
            history.ActorUserId, history.Reason, history.OccurredAt));
    }

    public async Task<Result<IReadOnlyList<AssetStatusHistoryDto>>> GetHistoryAsync(
        Guid assetId, CancellationToken ct = default)
    {
        var asset = await _assetRepository.GetByIdAsync(assetId, ct);
        if (asset is null)
            return Result.Failure<IReadOnlyList<AssetStatusHistoryDto>>(
                new Error("ASSETS.ASSET_NOT_FOUND", "Asset not found.", ErrorType.NotFound));

        var history = await _historyRepository.GetByAssetIdAsync(assetId, ct);
        var dtos = history.Select(h => new AssetStatusHistoryDto(
            h.Id, h.FromStatus, h.ToStatus, h.ActorUserId, h.Reason, h.OccurredAt)).ToList();

        return Result.Success<IReadOnlyList<AssetStatusHistoryDto>>(dtos);
    }

    /// <summary>
    /// State machine: defines which transitions are allowed from each status.
    /// Archived/Retired/Disposed are terminal for practical purposes — only Restore
    /// can bring an Archived asset back, and Disposed is fully terminal.
    /// </summary>
    private static bool IsTransitionAllowed(AssetStatus from, AssetStatus to)
    {
        return (from, to) switch
        {
            // InStock: can be assigned (→ InUse via AssignmentService), archived, or retired
            (AssetStatus.InStock, AssetStatus.Archived) => true,
            (AssetStatus.InStock, AssetStatus.Retired) => true,

            // InUse: can go to maintenance, be archived, retired, or transferred (stays InUse)
            (AssetStatus.InUse, AssetStatus.UnderMaintenance) => true,
            (AssetStatus.InUse, AssetStatus.Archived) => true,
            (AssetStatus.InUse, AssetStatus.Retired) => true,

            // UnderMaintenance: can return to InUse, be archived, or retired
            (AssetStatus.UnderMaintenance, AssetStatus.InUse) => true,
            (AssetStatus.UnderMaintenance, AssetStatus.Archived) => true,
            (AssetStatus.UnderMaintenance, AssetStatus.Retired) => true,

            // Archived: can be restored to InStock
            (AssetStatus.Archived, AssetStatus.InStock) => true,

            // Retired: can be disposed
            (AssetStatus.Retired, AssetStatus.Disposed) => true,

            // Disposed: terminal — no transitions out
            _ => false,
        };
    }
}
