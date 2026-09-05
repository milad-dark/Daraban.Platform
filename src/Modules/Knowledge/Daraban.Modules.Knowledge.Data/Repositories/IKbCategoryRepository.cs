using Daraban.Modules.Knowledge.Data.Entities;

namespace Daraban.Modules.Knowledge.Data.Repositories;

public interface IKbCategoryRepository
{
    Task<KbCategory?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<KbCategory>> GetAllAsync(Guid entityNodeId, bool includeInactive, CancellationToken ct = default);
    Task<IReadOnlyList<KbCategory>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);

    /// <summary>Walks up the ParentId chain from <paramref name="startId"/> and returns every
    /// ancestor id. Used to reject cycles when re-parenting.</summary>
    Task<IReadOnlyList<Guid>> GetAncestorIdsAsync(Guid startId, CancellationToken ct = default);

    Task<bool> HasChildrenAsync(Guid id, CancellationToken ct = default);
    Task<int> CountArticlesAsync(Guid categoryId, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, Guid entityNodeId, Guid? excludeId = null, CancellationToken ct = default);

    Task AddAsync(KbCategory category, CancellationToken ct = default);
    void Update(KbCategory category);
    Task SaveChangesAsync(CancellationToken ct = default);
}
