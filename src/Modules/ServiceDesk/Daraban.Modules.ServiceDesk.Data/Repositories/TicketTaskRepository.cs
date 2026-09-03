using Daraban.Modules.ServiceDesk.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.ServiceDesk.Data.Repositories;

public class TicketTaskRepository : ITicketTaskRepository
{
    private readonly ServiceDeskDbContext _context;

    public TicketTaskRepository(ServiceDeskDbContext context)
    {
        _context = context;
    }

    public async Task<TicketTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.TicketTasks.FindAsync(new object[] { id }, ct);
    }

    public async Task<IReadOnlyList<TicketTask>> GetByTicketIdAsync(Guid ticketId, CancellationToken ct = default)
    {
        return await _context.TicketTasks
            .Where(t => t.TicketId == ticketId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(TicketTask task, CancellationToken ct = default)
    {
        await _context.TicketTasks.AddAsync(task, ct);
    }

    public async Task UpdateAsync(TicketTask task, CancellationToken ct = default)
    {
        _context.TicketTasks.Update(task);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.TicketTasks.AnyAsync(t => t.Id == id, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
