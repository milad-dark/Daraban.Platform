using Daraban.Platform.Common;

namespace Daraban.Modules.ServiceDesk.Data.Entities;

/// <summary>
/// Work unit within a ticket — represents a single action, comment, or update.
/// Follows GLPI's tickettask model with time tracking.
/// </summary>
public class TicketTask : BaseEntity
{
    /// <summary>Reference to the parent ticket.</summary>
    public Guid TicketId { get; set; }

    /// <summary>User who created this task/comment.</summary>
    public Guid UserId { get; set; }

    /// <summary>Task content (HTML allowed).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Type of task (comment, action, status change).</summary>
    public TicketTaskType Type { get; set; } = TicketTaskType.Comment;

    /// <summary>Status before this task.</summary>
    public TicketStatus? PreviousStatus { get; set; }

    /// <summary>Status after this task.</summary>
    public TicketStatus? NewStatus { get; set; }

    /// <summary>Time spent in minutes (optional).</summary>
    public int? TimeSpentMinutes { get; set; }

    /// <summary>Whether this is a private comment (visible only to agents).</summary>
    public bool IsPrivate { get; set; } = false;

    /// <summary>Date this task was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation property
    public Ticket Ticket { get; set; } = null!;
}

public enum TicketTaskType
{
    Comment = 1,
    Action = 2,
    StatusChange = 3,
    Assignment = 4
}
