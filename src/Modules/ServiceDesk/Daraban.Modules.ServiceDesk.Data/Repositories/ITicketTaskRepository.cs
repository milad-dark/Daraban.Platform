using Daraban.Modules.ServiceDesk.Data.Entities;

namespace Daraban.Modules.ServiceDesk.Data.Repositories;

public interface ITicketTaskRepository
{
    Task<TicketTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TicketTask>> GetByTicketIdAsync(Guid ticketId, CancellationToken ct = default);
    Task AddAsync(TicketTask task, CancellationToken ct = default);
    Task UpdateAsync(TicketTask task, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
