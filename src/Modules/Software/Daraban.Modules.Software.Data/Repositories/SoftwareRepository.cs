using Daraban.Modules.Software.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Software.Data.Repositories;

public class SoftwareRepository : ISoftwareRepository
{
    private readonly SoftwareDbContext _context;

    public SoftwareRepository(SoftwareDbContext context)
    {
        _context = context;
    }

    public async Task<Software?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Softwares.FindAsync(new object[] { id }, ct);
    }

    public async Task<Software?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Softwares
            .Include(s => s.Licenses)
            .Include(s => s.Installations)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<(IReadOnlyList<Software> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        SoftwareCategory? category,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Softwares
            .Where(s => s.EntityId == entityNodeId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) || (s.Editor != null && s.Editor.Contains(search)));

        if (category.HasValue)
            query = query.Where(s => s.Category == category.Value);

        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Software software, CancellationToken ct = default)
    {
        await _context.Softwares.AddAsync(software, ct);
    }

    public async Task UpdateAsync(Software software, CancellationToken ct = default)
    {
        _context.Softwares.Update(software);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Softwares.AnyAsync(s => s.Id == id, ct);
    }

    public async Task<bool> NameExistsAsync(string name, Guid entityNodeId, Guid? excludeId = null, CancellationToken ct = default)
    {
        return await _context.Softwares
            .AnyAsync(s => s.Name == name && s.EntityId == entityNodeId && (excludeId == null || s.Id != excludeId), ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
