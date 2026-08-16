using Daraban.Modules.Identity.Data;
using Daraban.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Identity.Services.Authorization;

/// <summary>
/// Materialized-path prefix match (Task 1.2 SS9), not a recursive CTE -- cheaper, and it's
/// the reason EntityNode.FullPath exists and is indexed at all.
/// </summary>
public class EntityScopeAccessor : IEntityScopeAccessor
{
    private readonly IdentityDbContext _db;
    public EntityScopeAccessor(IdentityDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<Guid>> GetScopedEntityIdsAsync(Guid rootEntityId, bool recursive, CancellationToken ct = default)
    {
        if (!recursive)
            return [rootEntityId];

        var root = await _db.Entities.AsNoTracking().FirstOrDefaultAsync(e => e.Id == rootEntityId, ct);
        if (root is null)
            return [rootEntityId]; // unknown entity -- don't silently widen scope, just return the (useless) single id

        return await _db.Entities.AsNoTracking()
            .Where(e => e.FullPath.StartsWith(root.FullPath))
            .Select(e => e.Id)
            .ToListAsync(ct);
    }
}
