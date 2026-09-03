using Daraban.Platform.Common;

namespace Daraban.Modules.ServiceDesk.Data.Entities;

/// <summary>
/// Predefined template for common ticket types.
/// Allows quick creation of tickets with pre-filled fields.
/// </summary>
public class TicketTemplate : TenantScopedEntity
{
    /// <summary>Template name (e.g., "New User Onboarding", "Password Reset").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description of when to use this template.</summary>
    public string? Description { get; set; }

    /// <summary>Default ticket type.</summary>
    public TicketType DefaultType { get; set; } = TicketType.Incident;

    /// <summary>Default priority.</summary>
    public TicketPriority DefaultPriority { get; set; } = TicketPriority.Medium;

    /// <summary>Default impact.</summary>
    public TicketImpact DefaultImpact { get; set; } = TicketImpact.Medium;

    /// <summary>Default urgency.</summary>
    public TicketUrgency DefaultUrgency { get; set; } = TicketUrgency.Medium;

    /// <summary>Default title template (can include placeholders like {username}).</summary>
    public string? TitleTemplate { get; set; }

    /// <summary>Default description template.</summary>
    public string? DescriptionTemplate { get; set; }

    /// <summary>Default category.</summary>
    public Guid? DefaultCategoryId { get; set; }

    /// <summary>Default assignee.</summary>
    public Guid? DefaultAssignedUserId { get; set; }

    /// <summary>Default assignee group.</summary>
    public Guid? DefaultAssignedGroupId { get; set; }

    /// <summary>Whether this template is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Sort order for display.</summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>Custom fields as JSON (for dynamic fields).</summary>
    public string? CustomFields { get; set; }
}
