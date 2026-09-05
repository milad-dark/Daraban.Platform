using Daraban.Platform.Common;
using NpgsqlTypes;

namespace Daraban.Modules.Knowledge.Data.Entities;

/// <summary>
/// A knowledge-base article (Task 6.4). Content is Markdown -- rendered client-side, never
/// stored as HTML, so nothing here has to be HTML-sanitised on the way out.
///
/// <see cref="SearchVector"/> is a PostgreSQL <c>tsvector</c> GENERATED ALWAYS column, not a
/// value this code ever assigns: Postgres recomputes it from title+content on every write and
/// a GIN index over it backs GET /api/v1/kb/articles/search. That keeps full-text search
/// inside the database with no Elasticsearch dependency (Task 6.4 constraint).
/// </summary>
public class KbArticle : TenantScopedEntity
{
    /// <summary>Article title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Body, in Markdown.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Short teaser used in search results and category listings.</summary>
    public string? Summary { get; set; }

    /// <summary>Owning category. Null means uncategorised.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Draft / Published / Archived. Only Published articles are visible to
    /// non-authors (enforced in KbArticleService, see GetPublishedAsync).</summary>
    public KbArticleStatus Status { get; set; } = KbArticleStatus.Draft;

    /// <summary>Surfaced in the FAQ section of the self-service portal.</summary>
    public bool IsFaq { get; set; }

    /// <summary>Author (the user who created the article).</summary>
    public Guid AuthorUserId { get; set; }

    /// <summary>When the article was last moved into Published.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Who published it.</summary>
    public Guid? PublishedByUserId { get; set; }

    /// <summary>Read counter, incremented by the read endpoint.</summary>
    public int ViewCount { get; set; }

    /// <summary>Denormalised tally of KbFeedback rows where IsHelpful is true.</summary>
    public int HelpfulCount { get; set; }

    /// <summary>Denormalised tally of KbFeedback rows where IsHelpful is false.</summary>
    public int NotHelpfulCount { get; set; }

    /// <summary>Comma-separated free-text tags. Deliberately not a join table -- tags here are
    /// a search aid, not an entity anything else references.</summary>
    public string? Tags { get; set; }

    /// <summary>
    /// PostgreSQL tsvector, GENERATED ALWAYS from Title + Content. Never assigned in code;
    /// EF Core is told the column is store-generated (see KbArticleConfiguration).
    /// </summary>
    public NpgsqlTsVector SearchVector { get; set; } = null!;

    // Navigation properties
    public KbCategory? Category { get; set; }
    public ICollection<KbArticleTarget> Targets { get; set; } = new List<KbArticleTarget>();
    public ICollection<KbFeedback> Feedback { get; set; } = new List<KbFeedback>();
    public ICollection<KbTicketLink> TicketLinks { get; set; } = new List<KbTicketLink>();
}

public enum KbArticleStatus
{
    Draft = 1,
    Published = 2,
    Archived = 3
}
