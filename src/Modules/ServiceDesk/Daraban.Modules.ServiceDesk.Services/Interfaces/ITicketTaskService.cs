using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.ServiceDesk.Services.Interfaces;

public interface ITicketTaskService
{
    Task<Result<IReadOnlyList<TicketTaskDto>>> GetByTicketIdAsync(Guid ticketId, CancellationToken ct = default);
    Task<Result<TicketTaskDto>> CreateAsync(Guid ticketId, CreateTicketTaskRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
}
