using Daraban.Platform.Common;

namespace Daraban.Modules.Knowledge.Data.Entities;

/// <summary>
/// Links a KB article to a ticket (Task 6.4). Backs
/// POST /api/v1/tickets/{id}/solution.
///
/// TicketId is a bare Guid, not a foreign key: Ticket lives in the ServiceDesk module and a
/// Knowledge FK into servicedesk.* would violate the module-boundary rule (Task 1.1 SS3).
/// The ticket's own resolution field is updated by ServiceDesk in response to
/// KbArticleLinkedToTicketEvent, not by this module writing across the boundary.
///
/// At most one row per ticket may have IsSolution = true (partial unique index).
/// </summary>
public class KbTicketLink : BaseEntity
{
    /// <summary>Article being linked.</summary>
    public Guid ArticleId { get; set; }

    /// <summary>Ticket the article is linked from (servicedesk.tickets.id -- no FK by design).</summary>
    public Guid TicketId { get; set; }

    /// <summary>True when this article is the ticket's accepted resolution, as opposed to a
    /// merely-related reference.</summary>
    public bool IsSolution { get; set; }

    /// <summary>Who created the link.</summary>
    public Guid LinkedByUserId { get; set; }

    /// <summary>When the link was created.</summary>
    public DateTimeOffset LinkedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional note explaining why this article resolves the ticket.</summary>
    public string? Note { get; set; }

    // Navigation property
    public KbArticle Article { get; set; } = null!;
}
