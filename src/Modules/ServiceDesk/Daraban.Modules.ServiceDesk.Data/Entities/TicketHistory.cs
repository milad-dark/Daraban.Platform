using Daraban.Platform.Common;

namespace Daraban.Modules.ServiceDesk.Data.Entities;

/// <summary>
/// Audit trail for ticket changes — tracks who changed what and when.
/// </summary>
public class TicketHistory : BaseEntity
{
    /// <summary>Reference to the parent ticket.</summary>
    public Guid TicketId { get; set; }

    /// <summary>User who made the change.</summary>
    public Guid UserId { get; set; }

    /// <summary>Field that was changed.</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>Previous value (before change).</summary>
    public string? OldValue { get; set; }

    /// <summary>New value (after change).</summary>
    public string? NewValue { get; set; }

    /// <summary>Type of change (create, update, delete).</summary>
    public TicketHistoryAction Action { get; set; } = TicketHistoryAction.Update;

    /// <summary>Date the change occurred.</summary>
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional comment about the change.</summary>
    public string? Comment { get; set; }

    // Navigation property
    public Ticket Ticket { get; set; } = null!;
}

public enum TicketHistoryAction
{
    Create = 1,
    Update = 2,
    Delete = 3,
    StatusChange = 4,
    Assignment = 5
}
