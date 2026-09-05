using Daraban.Modules.ServiceDesk.Data.Entities;

namespace Daraban.Modules.ServiceDesk.Data.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Ticket> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        TicketType? type,
        TicketStatus? status,
        TicketPriority? priority,
        Guid? assignedUserId,
        Guid? assignedGroupId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<Ticket?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Ticket ticket, CancellationToken ct = default);
    Task UpdateAsync(Ticket ticket, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Counts tickets still awaiting work -- every status in
    /// <see cref="TicketStatuses.Open"/>, not just New. Previously the service asked
    /// GetPagedAsync for a single page and read its TotalCount, which both under-counted (New
    /// only) and made the database sort and page rows nobody looked at.
    /// </summary>
    Task<int> CountOpenAsync(Guid entityNodeId, CancellationToken ct = default);

    /// <summary>
    /// Counts open tickets whose SLA due date has already passed. Tickets with no DueDate cannot
    /// be overdue and are excluded.
    /// </summary>
    Task<int> CountOverdueAsync(Guid entityNodeId, DateTimeOffset asOf, CancellationToken ct = default);

    /// <summary>Appends an audit row. Committed by the caller's SaveChangesAsync, so the ticket
    /// mutation and its history entry land in the same transaction.</summary>
    Task AddHistoryAsync(TicketHistory history, CancellationToken ct = default);

    Task<IReadOnlyList<TicketHistory>> GetHistoryAsync(Guid ticketId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
