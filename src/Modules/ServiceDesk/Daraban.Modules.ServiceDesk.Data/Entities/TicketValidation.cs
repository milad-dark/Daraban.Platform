using Daraban.Platform.Common;

namespace Daraban.Modules.ServiceDesk.Data.Entities;

/// <summary>
/// Approval step for change tickets.
/// Tracks who needs to approve and their decision.
/// </summary>
public class TicketValidation : BaseEntity
{
    /// <summary>Reference to the parent ticket.</summary>
    public Guid TicketId { get; set; }

    /// <summary>User who needs to approve.</summary>
    public Guid UserId { get; set; }

    /// <summary>Validation status.</summary>
    public TicketValidationItemStatus Status { get; set; } = TicketValidationItemStatus.Waiting;

    /// <summary>Optional comment from the approver.</summary>
    public string? Comment { get; set; }

    /// <summary>Date when the validation was submitted.</summary>
    public DateTimeOffset? ValidatedAt { get; set; }

    /// <summary>Validation step number (for ordered approvals).</summary>
    public int StepNumber { get; set; } = 1;

    /// <summary>Whether this is a mandatory approval.</summary>
    public bool IsMandatory { get; set; } = true;

    // Navigation property
    public Ticket Ticket { get; set; } = null!;
}

public enum TicketValidationItemStatus
{
    Waiting = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}
