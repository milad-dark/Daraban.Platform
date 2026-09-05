using Daraban.Platform.Common;

namespace Daraban.Modules.Knowledge.Data.Entities;

/// <summary>
/// One reader's verdict on an article (Task 6.4: helpful/not-helpful + comment).
///
/// One row per user per article -- a second submission updates the existing row rather than
/// stacking, which is why (article_id, user_id) is a unique index. KbArticle.HelpfulCount /
/// NotHelpfulCount are maintained alongside these rows so listing articles never has to
/// aggregate this table.
/// </summary>
public class KbFeedback : BaseEntity
{
    /// <summary>Article being rated.</summary>
    public Guid ArticleId { get; set; }

    /// <summary>User who left the feedback.</summary>
    public Guid UserId { get; set; }

    /// <summary>True = helpful, false = not helpful.</summary>
    public bool IsHelpful { get; set; }

    /// <summary>Optional free-text comment.</summary>
    public string? Comment { get; set; }

    /// <summary>When the feedback was submitted (or last revised).</summary>
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation property
    public KbArticle Article { get; set; } = null!;
}
