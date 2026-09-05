using Daraban.Modules.Knowledge.Data.Entities;

namespace Daraban.Modules.Knowledge.Data.Repositories;

public interface IKbFeedbackRepository
{
    Task<KbFeedback?> GetByArticleAndUserAsync(Guid articleId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<KbFeedback>> GetByArticleAsync(Guid articleId, CancellationToken ct = default);
    Task<(int Helpful, int NotHelpful)> CountVerdictsAsync(Guid articleId, CancellationToken ct = default);

    Task AddAsync(KbFeedback feedback, CancellationToken ct = default);
    void Update(KbFeedback feedback);
    Task SaveChangesAsync(CancellationToken ct = default);
}
