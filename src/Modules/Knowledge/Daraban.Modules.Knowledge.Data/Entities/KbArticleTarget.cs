using Daraban.Platform.Common;

namespace Daraban.Modules.Knowledge.Data.Entities;

/// <summary>
/// Audience targeting for an article (Task 6.4: "visible to: all/group/entity").
///
/// An article with zero target rows is visible to every authenticated caller inside its own
/// entity. Adding rows narrows it: the caller must match at least one target. TargetId is
/// deliberately a bare Guid rather than a foreign key -- Group and EntityNode live in the
/// Identity module, and a Knowledge FK into identity.* would violate the module-boundary rule
/// (Task 1.1 SS3). Referential integrity for the Group/Entity cases is therefore the
/// application's job, not the database's.
/// </summary>
public class KbArticleTarget : BaseEntity
{
    /// <summary>Article this target applies to.</summary>
    public Guid ArticleId { get; set; }

    /// <summary>What kind of audience TargetId names.</summary>
    public KbTargetType TargetType { get; set; } = KbTargetType.All;

    /// <summary>Group id or entity-node id, depending on TargetType. Null when TargetType is All.</summary>
    public Guid? TargetId { get; set; }

    /// <summary>True when the target is an entity node and the article should also be visible
    /// to that node's descendants.</summary>
    public bool IsRecursive { get; set; }

    // Navigation property
    public KbArticle Article { get; set; } = null!;
}

public enum KbTargetType
{
    /// <summary>Everyone within the article's own entity.</summary>
    All = 1,

    /// <summary>A single Identity group (identity.groups.id).</summary>
    Group = 2,

    /// <summary>A single entity node (identity.entities.id), optionally recursive.</summary>
    Entity = 3,

    /// <summary>One named user (identity.users.id) -- used for articles under review.</summary>
    User = 4
}
