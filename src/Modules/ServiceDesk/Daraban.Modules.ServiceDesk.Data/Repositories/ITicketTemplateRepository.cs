using Daraban.Modules.ServiceDesk.Data.Entities;

namespace Daraban.Modules.ServiceDesk.Data.Repositories;

public interface ITicketTemplateRepository
{
    Task<TicketTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TicketTemplate>> GetAllAsync(Guid entityNodeId, CancellationToken ct = default);
    Task AddAsync(TicketTemplate template, CancellationToken ct = default);
    Task UpdateAsync(TicketTemplate template, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, Guid entityNodeId, Guid? excludeId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
