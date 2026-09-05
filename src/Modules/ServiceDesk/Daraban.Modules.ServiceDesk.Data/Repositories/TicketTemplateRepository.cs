using Daraban.Modules.ServiceDesk.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.ServiceDesk.Data.Repositories;

public class TicketTemplateRepository : ITicketTemplateRepository
{
    private readonly ServiceDeskDbContext _context;

    public TicketTemplateRepository(ServiceDeskDbContext context)
    {
        _context = context;
    }

    public async Task<TicketTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.TicketTemplates.FindAsync(new object[] { id }, ct);
    }

    public async Task<IReadOnlyList<TicketTemplate>> GetAllAsync(
        Guid entityNodeId, bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _context.TicketTemplates.AsNoTracking()
            .Where(t => t.EntityId == entityNodeId);

        if (!includeInactive)
            query = query.Where(t => t.IsActive);

        return await query
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(TicketTemplate template, CancellationToken ct = default)
    {
        await _context.TicketTemplates.AddAsync(template, ct);
    }

    public async Task UpdateAsync(TicketTemplate template, CancellationToken ct = default)
    {
        _context.TicketTemplates.Update(template);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.TicketTemplates.AnyAsync(t => t.Id == id, ct);
    }

    public async Task<bool> NameExistsAsync(string name, Guid entityNodeId, Guid? excludeId, CancellationToken ct = default)
    {
        // `t.Id != excludeId` would compare Guid to Guid? and translate to `id IS NOT NULL` when
        // excludeId is null -- true for every row, so it happened to work, but only by accident.
        // Being explicit keeps the create path (no exclusion) and the update path (exclude self)
        // obviously correct.
        return await _context.TicketTemplates
            .AnyAsync(t => t.Name == name
                           && t.EntityId == entityNodeId
                           && (excludeId == null || t.Id != excludeId.Value), ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
