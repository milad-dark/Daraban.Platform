using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.ServiceDesk.Services.Interfaces;

public interface ITicketTemplateService
{
    /// <summary><paramref name="includeInactive"/> is what makes a deactivated template findable
    /// again -- without it the admin screen could switch one off and never switch it back on.</summary>
    Task<Result<IReadOnlyList<TicketTemplateDto>>> GetAllAsync(
        Guid entityNodeId, bool includeInactive = false, CancellationToken ct = default);

    Task<Result<TicketTemplateDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<TicketTemplateDto>> CreateAsync(CreateTicketTemplateRequest request, Guid entityNodeId, Guid actorUserId, CancellationToken ct = default);
    Task<Result<TicketTemplateDto>> UpdateAsync(Guid id, UpdateTicketTemplateRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
}
