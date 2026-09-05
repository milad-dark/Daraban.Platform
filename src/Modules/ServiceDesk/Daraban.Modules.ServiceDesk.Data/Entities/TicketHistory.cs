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

    /// <summary>
    /// Builds one audit row. Every service that mutates a ticket goes through this so the shape of
    /// the audit trail is identical regardless of which code path wrote it -- and so adding a
    /// mutation without recording it becomes a visible omission rather than the default.
    /// </summary>
    public static TicketHistory Record(
        Guid ticketId,
        Guid actorUserId,
        string fieldName,
        string? oldValue,
        string? newValue,
        TicketHistoryAction action,
        string? comment = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new TicketHistory
        {
            Id = Guid.CreateVersion7(),
            TicketId = ticketId,
            UserId = actorUserId,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Action = action,
            OccurredAt = now,
            Comment = comment,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
        };
    }
}

public enum TicketHistoryAction
{
    Create = 1,
    Update = 2,
    Delete = 3,
    StatusChange = 4,
    Assignment = 5
}
