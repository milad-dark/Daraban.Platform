using Daraban.Modules.Assets.Data.Entities;
namespace Daraban.Modules.Assets.Data.Repositories;

public interface IAssetAssignmentRepository
{
    Task<AssetAssignment?> GetCurrentAsync(Guid assetId, CancellationToken ct = default);
    Task<IReadOnlyList<AssetAssignment>> GetHistoryAsync(Guid assetId, CancellationToken ct = default);
    Task<IReadOnlyList<AssetAssignment>> GetByTargetAsync(AssignmentTargetType targetType, Guid targetId, CancellationToken ct = default);
    Task AddAsync(AssetAssignment assignment, CancellationToken ct = default);
    Task UpdateAsync(AssetAssignment assignment, CancellationToken ct = default);
}
