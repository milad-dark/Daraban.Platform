using Daraban.Modules.Knowledge.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Knowledge.Data.Repositories;

public class KbCategoryRepository : IKbCategoryRepository
{
    /// <summary>Hard ceiling on the ancestor walk -- a corrupted parent chain must not spin forever.</summary>
    private const int MaxTreeDepth = 32;

    private readonly KnowledgeDbContext _context;

    public KbCategoryRepository(KnowledgeDbContext context) => _context = context;

    public async Task<KbCategory?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<KbCategory>> GetAllAsync(Guid entityNodeId, bool includeInactive, CancellationToken ct = default)
    {
        var query = _context.Categories.AsNoTracking().Where(c => c.EntityId == entityNodeId);

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<KbCategory>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
        => await _context.Categories.AsNoTracking()
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetAncestorIdsAsync(Guid startId, CancellationToken ct = default)
    {
        // Iterative walk rather than a recursive CTE: the tree is shallow (a KB taxonomy, not a
        // CMDB), and this keeps the repository free of raw SQL. MaxTreeDepth bounds it either way.
        var ancestors = new List<Guid>();
        var seen = new HashSet<Guid> { startId };

        var currentId = await _context.Categories.AsNoTracking()
            .Where(c => c.Id == startId)
            .Select(c => c.ParentId)
            .FirstOrDefaultAsync(ct);

        var depth = 0;
        while (currentId is not null && depth++ < MaxTreeDepth)
        {
            if (!seen.Add(currentId.Value))
                break; // pre-existing cycle in the data -- stop rather than loop

            ancestors.Add(currentId.Value);

            currentId = await _context.Categories.AsNoTracking()
                .Where(c => c.Id == currentId.Value)
                .Select(c => c.ParentId)
                .FirstOrDefaultAsync(ct);
        }

        return ancestors;
    }

    public async Task<bool> HasChildrenAsync(Guid id, CancellationToken ct = default)
        => await _context.Categories.AnyAsync(c => c.ParentId == id, ct);

    public async Task<int> CountArticlesAsync(Guid categoryId, CancellationToken ct = default)
        => await _context.Articles.CountAsync(a => a.CategoryId == categoryId, ct);

    public async Task<bool> SlugExistsAsync(string slug, Guid entityNodeId, Guid? excludeId = null, CancellationToken ct = default)
        => await _context.Categories.AnyAsync(
            c => c.Slug == slug && c.EntityId == entityNodeId && (excludeId == null || c.Id != excludeId),
            ct);

    public async Task AddAsync(KbCategory category, CancellationToken ct = default)
        => await _context.Categories.AddAsync(category, ct);

    public void Update(KbCategory category) => _context.Categories.Update(category);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
