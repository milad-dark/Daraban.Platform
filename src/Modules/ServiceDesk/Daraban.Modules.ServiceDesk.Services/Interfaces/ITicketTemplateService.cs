using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.ServiceDesk.Services.Interfaces;

public interface ITicketTemplateService
{
    Task<Result<IReadOnlyList<TicketTemplateDto>>> GetAllAsync(Guid entityNodeId, CancellationToken ct = default);
    Task<Result<TicketTemplateDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<TicketTemplateDto>> CreateAsync(CreateTicketTemplateRequest request, Guid entityNodeId, Guid actorUserId, CancellationToken ct = default);
    Task<Result<TicketTemplateDto>> UpdateAsync(Guid id, UpdateTicketTemplateRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
}
