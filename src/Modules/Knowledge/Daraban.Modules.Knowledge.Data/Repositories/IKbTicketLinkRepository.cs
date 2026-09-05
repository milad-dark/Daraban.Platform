using Daraban.Modules.Knowledge.Data.Entities;

namespace Daraban.Modules.Knowledge.Data.Repositories;

public interface IKbTicketLinkRepository
{
    Task<KbTicketLink?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<KbTicketLink?> GetAsync(Guid ticketId, Guid articleId, CancellationToken ct = default);
    Task<KbTicketLink?> GetSolutionAsync(Guid ticketId, CancellationToken ct = default);
    Task<IReadOnlyList<KbTicketLink>> GetByTicketAsync(Guid ticketId, CancellationToken ct = default);
    Task<IReadOnlyList<KbTicketLink>> GetByArticleAsync(Guid articleId, CancellationToken ct = default);

    Task AddAsync(KbTicketLink link, CancellationToken ct = default);
    void Update(KbTicketLink link);
    void Remove(KbTicketLink link);
    Task SaveChangesAsync(CancellationToken ct = default);
}
