using Daraban.Modules.Knowledge.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Knowledge.Data.Repositories;

public class KbArticleRepository : IKbArticleRepository
{
    private readonly KnowledgeDbContext _context;

    public KbArticleRepository(KnowledgeDbContext context) => _context = context;

    public async Task<KbArticle?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Articles.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<KbArticle?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _context.Articles
            .Include(a => a.Category)
            .Include(a => a.Targets)
            .Include(a => a.TicketLinks)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<(IReadOnlyList<KbArticle> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        Guid? categoryId,
        KbArticleStatus? status,
        bool? isFaq,
        Guid? authorUserId,
        string? titleContains,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Articles.AsNoTracking().Where(a => a.EntityId == entityNodeId);

        if (categoryId.HasValue)
            query = query.Where(a => a.CategoryId == categoryId.Value);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (isFaq.HasValue)
            query = query.Where(a => a.IsFaq == isFaq.Value);

        if (authorUserId.HasValue)
            query = query.Where(a => a.AuthorUserId == authorUserId.Value);

        // Plain title filter for the list screen. Free-text relevance search is SearchAsync,
        // which goes through the tsvector index instead of an ILIKE scan.
        if (!string.IsNullOrWhiteSpace(titleContains))
            query = query.Where(a => EF.Functions.ILike(a.Title, $"%{titleContains}%"));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<KbSearchHit> Items, int TotalCount)> SearchAsync(
        Guid entityNodeId,
        string query,
        Guid? categoryId,
        KbArticleStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // websearch_to_tsquery, not to_tsquery: it accepts raw user input ("vpn OR wifi",
        // quoted phrases, bare words) without ever throwing a syntax error, so a stray
        // apostrophe in the query string can't turn into a 500.
        //
        // The EF.Functions call MUST appear inline inside each expression tree. Hoisting it into
        // a local (`var tsQuery = EF.Functions.WebSearchToTsQuery(...)`) invokes the method in
        // C# instead of translating it, and the shim throws
        // "not supported because the query has switched to client-evaluation".
        var baseQuery = _context.Articles.AsNoTracking()
            .Where(a => a.EntityId == entityNodeId
                        && a.SearchVector.Matches(EF.Functions.WebSearchToTsQuery("english", query)));

        if (categoryId.HasValue)
            baseQuery = baseQuery.Where(a => a.CategoryId == categoryId.Value);

        if (status.HasValue)
            baseQuery = baseQuery.Where(a => a.Status == status.Value);

        var totalCount = await baseQuery.CountAsync(ct);

        var hits = await baseQuery
            .Select(a => new
            {
                Article = a,
                Rank = a.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("english", query)),
            })
            .OrderByDescending(x => x.Rank)
            .ThenByDescending(x => x.Article.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = hits.Select(h => new KbSearchHit(h.Article, h.Rank)).ToList();
        return (items, totalCount);
    }

    public async Task<IReadOnlyList<KbArticleTarget>> GetTargetsAsync(Guid articleId, CancellationToken ct = default)
        => await _context.ArticleTargets.AsNoTracking()
            .Where(t => t.ArticleId == articleId)
            .ToListAsync(ct);

    public async Task ReplaceTargetsAsync(Guid articleId, IEnumerable<KbArticleTarget> targets, CancellationToken ct = default)
    {
        var existing = await _context.ArticleTargets
            .Where(t => t.ArticleId == articleId)
            .ToListAsync(ct);

        _context.ArticleTargets.RemoveRange(existing);
        await _context.ArticleTargets.AddRangeAsync(targets, ct);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => await _context.Articles.AnyAsync(a => a.Id == id, ct);

    public async Task IncrementViewCountAsync(Guid id, CancellationToken ct = default)
        // Server-side UPDATE ... SET view_count = view_count + 1. Doing this by loading the
        // entity and saving would lose increments whenever two readers overlap.
        => await _context.Articles
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ViewCount, a => a.ViewCount + 1), ct);

    public async Task AddAsync(KbArticle article, CancellationToken ct = default)
        => await _context.Articles.AddAsync(article, ct);

    public void Update(KbArticle article) => _context.Articles.Update(article);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
