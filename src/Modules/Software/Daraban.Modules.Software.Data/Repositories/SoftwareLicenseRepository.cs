using Daraban.Modules.Software.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Software.Data.Repositories;

public class SoftwareLicenseRepository : ISoftwareLicenseRepository
{
    private readonly SoftwareDbContext _context;

    public SoftwareLicenseRepository(SoftwareDbContext context)
    {
        _context = context;
    }

    public async Task<SoftwareLicense?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Licenses.FindAsync(new object[] { id }, ct);
    }

    public async Task<SoftwareLicense?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Licenses
            .Include(l => l.Software)
            .Include(l => l.Installations)
            .FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public async Task<(IReadOnlyList<SoftwareLicense> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        Guid? softwareId,
        LicenseType? type,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Licenses
            .Where(l => l.EntityId == entityNodeId);

        if (softwareId.HasValue)
            query = query.Where(l => l.SoftwareId == softwareId.Value);

        if (type.HasValue)
            query = query.Where(l => l.Type == type.Value);

        if (isActive.HasValue)
            query = query.Where(l => l.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(l => l.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<SoftwareLicense>> GetBySoftwareIdAsync(Guid softwareId, CancellationToken ct = default)
    {
        return await _context.Licenses
            .Where(l => l.SoftwareId == softwareId && l.IsActive)
            .OrderBy(l => l.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(SoftwareLicense license, CancellationToken ct = default)
    {
        await _context.Licenses.AddAsync(license, ct);
    }

    public async Task UpdateAsync(SoftwareLicense license, CancellationToken ct = default)
    {
        _context.Licenses.Update(license);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Licenses.AnyAsync(l => l.Id == id, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
