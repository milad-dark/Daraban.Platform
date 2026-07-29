namespace Daraban.Platform.Abstractions;

/// <summary>Resolves the entity-tree scope (self + recursive descendants/ancestors as applicable)
/// a caller is allowed to query against -- backs the entity_id filtering described in Task 1.2 SS11
/// and the permission resolution algorithm in Task 1.3 SS4.3.</summary>
public interface IEntityScopeAccessor
{
    Task<IReadOnlyCollection<Guid>> GetScopedEntityIdsAsync(Guid rootEntityId, bool recursive, CancellationToken ct = default);
}
