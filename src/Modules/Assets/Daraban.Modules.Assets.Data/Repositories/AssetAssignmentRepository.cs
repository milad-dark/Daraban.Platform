using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Assets.Data.Repositories;

public class AssetAssignmentRepository : IAssetAssignmentRepository
{
    private readonly AssetsDbContext _db;
    public AssetAssignmentRepository(AssetsDbContext db) => _db = db;

    public Task<AssetAssignment?> GetCurrentAsync(Guid assetId, CancellationToken ct = default)
        => _db.AssetAssignments
            .FirstOrDefaultAsync(a => a.AssetId == assetId && a.IsCurrent, ct);

    public async Task<IReadOnlyList<AssetAssignment>> GetHistoryAsync(Guid assetId, CancellationToken ct = default)
        => (await _db.AssetAssignments
            .Where(a => a.AssetId == assetId)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync(ct)).AsReadOnly();

    public async Task<IReadOnlyList<AssetAssignment>> GetByTargetAsync(AssignmentTargetType targetType, Guid targetId, CancellationToken ct = default)
        => (await _db.AssetAssignments
            .Where(a => a.TargetType == targetType && a.TargetId == targetId && a.IsCurrent)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync(ct)).AsReadOnly();

    public async Task AddAsync(AssetAssignment assignment, CancellationToken ct = default)
        => await _db.AssetAssignments.AddAsync(assignment, ct);

    public Task UpdateAsync(AssetAssignment assignment, CancellationToken ct = default)
    {
        _db.AssetAssignments.Update(assignment);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
