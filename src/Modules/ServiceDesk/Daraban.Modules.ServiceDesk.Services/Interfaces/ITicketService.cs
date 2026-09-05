using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.ServiceDesk.Services.Interfaces;

public interface ITicketService
{
    Task<Result<TicketPagedResult>> GetPagedAsync(
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

    Task<Result<TicketDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a ticket inside <paramref name="entityNodeId"/>. That parameter is not optional:
    /// the tenant scope has to come from the caller's resolved context, and an earlier version
    /// assigned the actor's user id to Ticket.EntityId, which put every new ticket in a
    /// nonexistent entity and hid it from every list query.
    /// </summary>
    Task<Result<TicketDto>> CreateAsync(
        CreateTicketRequest request, Guid entityNodeId, Guid actorUserId, CancellationToken ct = default);

    Task<Result<TicketDto>> UpdateAsync(Guid id, UpdateTicketRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
    Task<Result<TicketDto>> ChangeStatusAsync(Guid id, TicketStatus newStatus, Guid actorUserId, string? reason, CancellationToken ct = default);
    Task<Result<TicketDto>> AssignAsync(Guid id, Guid? assignedUserId, Guid? assignedGroupId, Guid actorUserId, CancellationToken ct = default);
    Task<Result<TicketDto>> EscalateAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
    Task<Result<TicketDto>> SolveAsync(Guid id, Guid actorUserId, string? solution, CancellationToken ct = default);
    Task<Result<TicketDto>> CloseAsync(Guid id, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Audit trail for one ticket, newest first.</summary>
    Task<Result<IReadOnlyList<TicketHistoryDto>>> GetHistoryAsync(Guid id, CancellationToken ct = default);

    Task<Result<int>> GetOpenCountAsync(Guid entityNodeId, CancellationToken ct = default);
    Task<Result<int>> GetOverdueCountAsync(Guid entityNodeId, CancellationToken ct = default);
}
