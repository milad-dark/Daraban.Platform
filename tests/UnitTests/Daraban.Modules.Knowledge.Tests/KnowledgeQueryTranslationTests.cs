using Daraban.Modules.Knowledge.Data;
using Daraban.Modules.Knowledge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Daraban.Modules.Knowledge.Tests;

/// <summary>
/// Translates the module's queries to SQL via ToQueryString() without opening a connection.
/// This is where the risky part of Task 6.4 gets proven: if SearchVector.Matches(...) or
/// .Rank(...) ever stopped being translatable by the Npgsql provider, EF would throw at
/// runtime on the first search request rather than at build time. These assertions catch that
/// in CI instead.
/// </summary>
public class KnowledgeQueryTranslationTests
{
    private static KnowledgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql("Host=localhost;Database=daraban_query_test;Username=x;Password=y")
            .Options;

        return new KnowledgeDbContext(options);
    }

    [Fact]
    public void FullTextSearch_Translates_To_Websearch_To_Tsquery_And_Ts_Rank()
    {
        using var context = CreateContext();
        var entityId = Guid.CreateVersion7();
        const string query = "vpn reset";

        // The EF.Functions call has to sit inline inside the expression tree, exactly as
        // KbArticleRepository.SearchAsync does it. Hoisting it into a local invokes the shim in
        // C# and throws "the query has switched to client-evaluation" -- this test exists
        // because that is precisely the mistake that would otherwise only surface as a 500 on
        // the first real search request.
        var sql = context.Articles.AsNoTracking()
            .Where(a => a.EntityId == entityId
                        && a.SearchVector.Matches(EF.Functions.WebSearchToTsQuery("english", query)))
            .Select(a => new
            {
                a.Id,
                Rank = a.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("english", query)),
            })
            .OrderByDescending(x => x.Rank)
            .ToQueryString();

        // websearch_to_tsquery, not to_tsquery: raw user input must never be able to produce a
        // syntax error.
        Assert.Contains("websearch_to_tsquery", sql);

        // The @@ operator is what actually uses ix_kb_articles_search_vector.
        Assert.Contains("search_vector", sql);
        Assert.Contains("@@", sql);

        // Relevance ordering, not just filtering.
        Assert.Contains("ts_rank", sql);
    }

    [Fact]
    public void Queries_Are_Scoped_To_The_Knowledge_Schema()
    {
        using var context = CreateContext();

        var sql = context.Articles.AsNoTracking().ToQueryString();

        Assert.Contains("knowledge.kb_articles", sql);
    }

    [Fact]
    public void SoftDeleteFilter_Is_Present_In_Generated_Sql()
    {
        using var context = CreateContext();

        var sql = context.Articles.AsNoTracking().ToQueryString();

        // If the query filter silently stopped applying, deleted articles would reappear in
        // every list and search result.
        Assert.Contains("is_deleted", sql);
    }

    [Fact]
    public void ChildTable_Queries_Filter_On_The_Parent_Articles_SoftDelete()
    {
        using var context = CreateContext();

        // Targets/feedback/links have no IsDeleted of their own -- they inherit the article's,
        // which means the generated SQL has to reach across to kb_articles.
        var targetsSql = context.ArticleTargets.AsNoTracking().ToQueryString();
        Assert.Contains("kb_articles", targetsSql);
        Assert.Contains("is_deleted", targetsSql);

        var feedbackSql = context.Feedback.AsNoTracking().ToQueryString();
        Assert.Contains("kb_articles", feedbackSql);

        var linksSql = context.TicketLinks.AsNoTracking().ToQueryString();
        Assert.Contains("kb_articles", linksSql);
    }

    [Fact]
    public void EntityStatus_ListQuery_Translates_Without_Client_Evaluation()
    {
        using var context = CreateContext();
        var entityId = Guid.CreateVersion7();

        // Mirrors KbArticleRepository.GetPagedAsync. ILike must translate to a server-side
        // ILIKE -- if it fell back to client evaluation EF would throw here.
        var sql = context.Articles.AsNoTracking()
            .Where(a => a.EntityId == entityId
                        && a.Status == KbArticleStatus.Published
                        && EF.Functions.ILike(a.Title, "%vpn%"))
            .OrderByDescending(a => a.UpdatedAt)
            .Skip(0)
            .Take(20)
            .ToQueryString();

        Assert.Contains("ILIKE", sql, StringComparison.OrdinalIgnoreCase);

        // Enum stored as text, so the parameter/literal must be the name, never an ordinal.
        Assert.Contains("status", sql);
        Assert.Contains("LIMIT", sql);
    }

    [Fact]
    public void FeedbackVerdictCount_GroupBy_Translates_To_A_Single_Statement()
    {
        using var context = CreateContext();
        var articleId = Guid.CreateVersion7();

        var sql = context.Feedback.AsNoTracking()
            .Where(f => f.ArticleId == articleId)
            .GroupBy(f => f.IsHelpful)
            .Select(g => new { IsHelpful = g.Key, Count = g.Count() })
            .ToQueryString();

        // One round trip rather than two COUNT queries.
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("count(", sql, StringComparison.OrdinalIgnoreCase);
    }
}
