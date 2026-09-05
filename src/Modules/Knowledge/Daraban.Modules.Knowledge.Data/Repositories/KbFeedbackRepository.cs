using Daraban.Modules.Knowledge.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Knowledge.Data.Repositories;

public class KbFeedbackRepository : IKbFeedbackRepository
{
    private readonly KnowledgeDbContext _context;

    public KbFeedbackRepository(KnowledgeDbContext context) => _context = context;

    public async Task<KbFeedback?> GetByArticleAndUserAsync(Guid articleId, Guid userId, CancellationToken ct = default)
        => await _context.Feedback.FirstOrDefaultAsync(f => f.ArticleId == articleId && f.UserId == userId, ct);

    public async Task<IReadOnlyList<KbFeedback>> GetByArticleAsync(Guid articleId, CancellationToken ct = default)
        => await _context.Feedback.AsNoTracking()
            .Where(f => f.ArticleId == articleId)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync(ct);

    public async Task<(int Helpful, int NotHelpful)> CountVerdictsAsync(Guid articleId, CancellationToken ct = default)
    {
        // Single round trip -- group by the verdict rather than issuing two COUNT queries.
        var counts = await _context.Feedback.AsNoTracking()
            .Where(f => f.ArticleId == articleId)
            .GroupBy(f => f.IsHelpful)
            .Select(g => new { IsHelpful = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var helpful = counts.FirstOrDefault(c => c.IsHelpful)?.Count ?? 0;
        var notHelpful = counts.FirstOrDefault(c => !c.IsHelpful)?.Count ?? 0;
        return (helpful, notHelpful);
    }

    public async Task AddAsync(KbFeedback feedback, CancellationToken ct = default)
        => await _context.Feedback.AddAsync(feedback, ct);

    public void Update(KbFeedback feedback) => _context.Feedback.Update(feedback);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
