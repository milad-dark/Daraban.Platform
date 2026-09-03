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
    Task<Result<TicketDto>> CreateAsync(CreateTicketRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result<TicketDto>> UpdateAsync(Guid id, UpdateTicketRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
    Task<Result<TicketDto>> ChangeStatusAsync(Guid id, TicketStatus newStatus, Guid actorUserId, string? reason, CancellationToken ct = default);
    Task<Result<TicketDto>> AssignAsync(Guid id, Guid? assignedUserId, Guid? assignedGroupId, Guid actorUserId, CancellationToken ct = default);
    Task<Result<TicketDto>> EscalateAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
    Task<Result<TicketDto>> SolveAsync(Guid id, Guid actorUserId, string? solution, CancellationToken ct = default);
    Task<Result<TicketDto>> CloseAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
    Task<Result<int>> GetOpenCountAsync(Guid entityNodeId, CancellationToken ct = default);
    Task<Result<int>> GetOverdueCountAsync(Guid entityNodeId, CancellationToken ct = default);
}
