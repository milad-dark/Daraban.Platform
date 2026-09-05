using Daraban.Modules.Knowledge.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Knowledge.Data.Repositories;

public class KbTicketLinkRepository : IKbTicketLinkRepository
{
    private readonly KnowledgeDbContext _context;

    public KbTicketLinkRepository(KnowledgeDbContext context) => _context = context;

    public async Task<KbTicketLink?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.TicketLinks.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<KbTicketLink?> GetAsync(Guid ticketId, Guid articleId, CancellationToken ct = default)
        => await _context.TicketLinks.FirstOrDefaultAsync(l => l.TicketId == ticketId && l.ArticleId == articleId, ct);

    public async Task<KbTicketLink?> GetSolutionAsync(Guid ticketId, CancellationToken ct = default)
        => await _context.TicketLinks.FirstOrDefaultAsync(l => l.TicketId == ticketId && l.IsSolution, ct);

    public async Task<IReadOnlyList<KbTicketLink>> GetByTicketAsync(Guid ticketId, CancellationToken ct = default)
        => await _context.TicketLinks.AsNoTracking()
            .Include(l => l.Article)
            .Where(l => l.TicketId == ticketId)
            .OrderByDescending(l => l.IsSolution)
            .ThenByDescending(l => l.LinkedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<KbTicketLink>> GetByArticleAsync(Guid articleId, CancellationToken ct = default)
        => await _context.TicketLinks.AsNoTracking()
            .Where(l => l.ArticleId == articleId)
            .OrderByDescending(l => l.LinkedAt)
            .ToListAsync(ct);

    public async Task AddAsync(KbTicketLink link, CancellationToken ct = default)
        => await _context.TicketLinks.AddAsync(link, ct);

    public void Update(KbTicketLink link) => _context.TicketLinks.Update(link);

    public void Remove(KbTicketLink link) => _context.TicketLinks.Remove(link);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
