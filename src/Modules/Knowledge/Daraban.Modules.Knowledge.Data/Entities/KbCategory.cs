using Daraban.Platform.Common;

namespace Daraban.Modules.Knowledge.Data.Entities;

/// <summary>
/// Recursive knowledge-base category tree (Task 6.4). Same self-referencing shape as
/// Assets.Location -- ParentId + Children, no materialised path column. Depth is bounded and
/// cycles are rejected in KbCategoryService, not by the database.
/// </summary>
public class KbCategory : TenantScopedEntity
{
    /// <summary>Parent category. Null for a root category.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-friendly identifier, unique per entity.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Optional description shown on category listings.</summary>
    public string? Description { get; set; }

    /// <summary>Manual ordering within the parent's children.</summary>
    public int SortOrder { get; set; }

    /// <summary>Inactive categories stay queryable but are hidden from the self-service portal.</summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public KbCategory? Parent { get; set; }
    public ICollection<KbCategory> Children { get; set; } = new List<KbCategory>();
    public ICollection<KbArticle> Articles { get; set; } = new List<KbArticle>();
}
