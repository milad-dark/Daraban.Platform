using Daraban.Modules.ServiceDesk.Data.Entities;

namespace Daraban.Modules.ServiceDesk.Data.Repositories;

public interface ITicketTemplateRepository
{
    Task<TicketTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lists templates for one entity. <paramref name="includeInactive"/> is needed by the
    /// admin screen -- filtering to active-only unconditionally made a deactivated template
    /// impossible to find and therefore impossible to reactivate.</summary>
    Task<IReadOnlyList<TicketTemplate>> GetAllAsync(
        Guid entityNodeId, bool includeInactive = false, CancellationToken ct = default);

    Task AddAsync(TicketTemplate template, CancellationToken ct = default);
    Task UpdateAsync(TicketTemplate template, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, Guid entityNodeId, Guid? excludeId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
