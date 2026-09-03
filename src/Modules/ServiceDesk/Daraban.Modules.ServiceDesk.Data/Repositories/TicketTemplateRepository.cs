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

    public async Task<IReadOnlyList<TicketTemplate>> GetAllAsync(Guid entityNodeId, CancellationToken ct = default)
    {
        return await _context.TicketTemplates
            .Where(t => t.EntityId == entityNodeId && t.IsActive)
            .OrderBy(t => t.SortOrder)
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
        return await _context.TicketTemplates
            .AnyAsync(t => t.Name == name && t.EntityId == entityNodeId && t.Id != excludeId, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
