using Daraban.Modules.ServiceDesk.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.ServiceDesk.Data.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly ServiceDeskDbContext _context;

    public TicketRepository(ServiceDeskDbContext context)
    {
        _context = context;
    }

    public async Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Tickets.FindAsync(new object[] { id }, ct);
    }

    public async Task<Ticket?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Tickets
            .Include(t => t.Tasks)
            .Include(t => t.Costs)
            .Include(t => t.History)
            .Include(t => t.Validations)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<(IReadOnlyList<Ticket> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        TicketType? type,
        TicketStatus? status,
        TicketPriority? priority,
        Guid? assignedUserId,
        Guid? assignedGroupId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Tickets
            .Where(t => t.EntityId == entityNodeId);

        if (type.HasValue)
            query = query.Where(t => t.Type == type.Value);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (priority.HasValue)
            query = query.Where(t => t.Priority == priority.Value);

        if (assignedUserId.HasValue)
            query = query.Where(t => t.AssignedUserId == assignedUserId.Value);

        if (assignedGroupId.HasValue)
            query = query.Where(t => t.AssignedGroupId == assignedGroupId.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Title.Contains(search));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Ticket ticket, CancellationToken ct = default)
    {
        await _context.Tickets.AddAsync(ticket, ct);
    }

    public async Task UpdateAsync(Ticket ticket, CancellationToken ct = default)
    {
        _context.Tickets.Update(ticket);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Tickets.AnyAsync(t => t.Id == id, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
