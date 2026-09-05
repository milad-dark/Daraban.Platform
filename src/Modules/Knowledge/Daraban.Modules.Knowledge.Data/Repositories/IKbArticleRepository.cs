using Daraban.Modules.Knowledge.Data.Entities;

namespace Daraban.Modules.Knowledge.Data.Repositories;

/// <summary>Result of one full-text search hit: the article plus its ts_rank score.</summary>
public sealed record KbSearchHit(KbArticle Article, double Rank);

public interface IKbArticleRepository
{
    Task<KbArticle?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Includes Category, Targets, and TicketLinks -- used by the detail endpoint.</summary>
    Task<KbArticle?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<KbArticle> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        Guid? categoryId,
        KbArticleStatus? status,
        bool? isFaq,
        Guid? authorUserId,
        string? titleContains,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// PostgreSQL full-text search over the generated tsvector column, ranked by ts_rank
    /// (Task 6.4: no Elasticsearch). Uses websearch_to_tsquery so raw user input never
    /// produces a syntax error the way to_tsquery would.
    /// </summary>
    Task<(IReadOnlyList<KbSearchHit> Items, int TotalCount)> SearchAsync(
        Guid entityNodeId,
        string query,
        Guid? categoryId,
        KbArticleStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<KbArticleTarget>> GetTargetsAsync(Guid articleId, CancellationToken ct = default);
    Task ReplaceTargetsAsync(Guid articleId, IEnumerable<KbArticleTarget> targets, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Atomic in-database increment, so concurrent reads don't lose counts to a
    /// read-modify-write race the way loading + saving the entity would.</summary>
    Task IncrementViewCountAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(KbArticle article, CancellationToken ct = default);
    void Update(KbArticle article);
    Task SaveChangesAsync(CancellationToken ct = default);
}
