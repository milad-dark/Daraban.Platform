using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services;

public class AssetAssignmentService(
    IAssetAssignmentRepository assignmentRepository,
    IAssetRepository assetRepository) : IAssetAssignmentService
{
    public async Task<Result<AssetAssignmentDto>> AssignAsync(Guid assetId, AssignAssetRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        // Asset must exist
        var asset = await assetRepository.GetByIdAsync(assetId, ct);
        if (asset is null)
            return Result.Failure<AssetAssignmentDto>(
                new Error("ASSETS.ASSET_NOT_FOUND", "Asset not found.", ErrorType.NotFound));

        // Cannot assign archived, retired, or disposed assets
        if (asset.Status is AssetStatus.Archived or AssetStatus.Retired or AssetStatus.Disposed)
            return Result.Failure<AssetAssignmentDto>(
                new Error("ASSETS.ASSET_NOT_ASSIGNABLE",
                    $"Cannot assign an asset with status '{asset.Status}'.", ErrorType.BusinessRule));

        // Unassign current assignment if one exists
        var current = await assignmentRepository.GetCurrentAsync(assetId, ct);
        if (current is not null)
        {
            current.IsCurrent = false;
            current.UnassignedAt = DateTimeOffset.UtcNow;
            await assignmentRepository.UpdateAsync(current, ct);
        }

        var now = DateTimeOffset.UtcNow;
        var assignment = new AssetAssignment
        {
            Id = Guid.CreateVersion7(),
            AssetId = assetId,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            TargetName = request.TargetName,
            AssignedAt = now,
            AssignedByUserId = actorUserId,
            Notes = request.Notes,
            IsCurrent = true,
        };

        await assignmentRepository.AddAsync(assignment, ct);

        // Transition asset status to InUse if currently InStock
        if (asset.Status == AssetStatus.InStock)
        {
            asset.Status = AssetStatus.InUse;
            asset.UpdatedAt = now;
        }

        await assetRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(assignment));
    }

    public async Task<Result> UnassignAsync(Guid assetId, Guid actorUserId, string? notes, CancellationToken ct = default)
    {
        var asset = await assetRepository.GetByIdAsync(assetId, ct);
        if (asset is null)
            return Result.Failure(
                new Error("ASSETS.ASSET_NOT_FOUND", "Asset not found.", ErrorType.NotFound));

        var current = await assignmentRepository.GetCurrentAsync(assetId, ct);
        if (current is null)
            return Result.Failure(
                new Error("ASSETS.ASSIGNMENT_NOT_FOUND",
                    "Asset has no current assignment.", ErrorType.NotFound));

        current.IsCurrent = false;
        current.UnassignedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(notes))
            current.Notes = notes;

        await assignmentRepository.UpdateAsync(current, ct);
        await assetRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<AssetAssignmentDto?>> GetCurrentAsync(Guid assetId, CancellationToken ct = default)
    {
        var asset = await assetRepository.GetByIdAsync(assetId, ct);
        if (asset is null)
            return Result.Failure<AssetAssignmentDto?>(
                new Error("ASSETS.ASSET_NOT_FOUND", "Asset not found.", ErrorType.NotFound));

        var assignment = await assignmentRepository.GetCurrentAsync(assetId, ct);
        return Result.Success(assignment is not null ? MapToDto(assignment) : null);
    }

    public async Task<Result<IReadOnlyList<AssetAssignmentDto>>> GetHistoryAsync(Guid assetId, CancellationToken ct = default)
    {
        var asset = await assetRepository.GetByIdAsync(assetId, ct);
        if (asset is null)
            return Result.Failure<IReadOnlyList<AssetAssignmentDto>>(
                new Error("ASSETS.ASSET_NOT_FOUND", "Asset not found.", ErrorType.NotFound));

        var history = await assignmentRepository.GetHistoryAsync(assetId, ct);
        var dtos = history.Select(MapToDto).ToList();
        return Result.Success<IReadOnlyList<AssetAssignmentDto>>(dtos);
    }

    public async Task<Result<IReadOnlyList<AssetListDto>>> GetByTargetAsync(AssignmentTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        var assignments = await assignmentRepository.GetByTargetAsync(targetType, targetId, ct);
        var assetIds = assignments.Select(a => a.AssetId).Distinct().ToList();

        var assets = new List<AssetListDto>();
        foreach (var id in assetIds)
        {
            var asset = await assetRepository.GetByIdAsync(id, ct);
            if (asset is not null)
                assets.Add(new AssetListDto(
                    asset.Id, asset.Name, asset.AssetTag, asset.SerialNumber,
                    asset.Status, asset.AssetType?.Name ?? string.Empty,
                    null, asset.WarrantyExpiry));
        }

        return Result.Success<IReadOnlyList<AssetListDto>>(assets);
    }

    private static AssetAssignmentDto MapToDto(AssetAssignment a) => new(
        a.Id,
        a.TargetType,
        a.TargetId,
        a.TargetName,
        a.AssignedAt,
        a.UnassignedAt,
        a.IsCurrent,
        a.Notes);
}
