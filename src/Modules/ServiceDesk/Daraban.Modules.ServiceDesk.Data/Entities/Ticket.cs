using Daraban.Platform.Common;

namespace Daraban.Modules.ServiceDesk.Data.Entities;

/// <summary>
/// Main ITIL object — represents Incidents, Requests, Problems, and Changes.
/// Follows GLPI's ticket model with ITIL categories and SLA tracking.
/// </summary>
public class Ticket : TenantScopedEntity
{
    /// <summary>ITIL type: Incident, Request, Problem, or Change.</summary>
    public TicketType Type { get; set; } = TicketType.Incident;

    /// <summary>Current workflow status.</summary>
    public TicketStatus Status { get; set; } = TicketStatus.New;

    /// <summary>Priority level (1=Low, 2=Medium, 3=High, 4=Very High, 5=Critical).</summary>
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    /// <summary>Impact level (1=Low, 2=Medium, 3=High).</summary>
    public TicketImpact Impact { get; set; } = TicketImpact.Medium;

    /// <summary>Urgency level (1=Low, 2=Medium, 3=High).</summary>
    public TicketUrgency Urgency { get; set; } = TicketUrgency.Medium;

    /// <summary>Auto-calculated from Priority * Impact * Urgency.</summary>
    public int? CalculatedScore { get; set; }

    /// <summary>Title/subject of the ticket.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed description (HTML allowed for rich text).</summary>
    public string? Description { get; set; }

    /// <summary>Date the ticket was opened.</summary>
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Date the ticket was last updated.</summary>
    public DateTimeOffset? LastUpdated { get; set; }

    /// <summary>Date the ticket was closed/resolved.</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>Date the ticket was solved (before closure validation).</summary>
    public DateTimeOffset? SolvedAt { get; set; }

    /// <summary>Due date based on SLA.</summary>
    public DateTimeOffset? DueDate { get; set; }

    /// <summary>Escalation level (0=Normal, 1=First, 2=Second).</summary>
    public int EscalationLevel { get; set; } = 0;

    /// <summary>Whether ticket is escalated.</summary>
    public bool IsEscalated { get; set; } = false;

    /// <summary>User who created the ticket (requester).</summary>
    public Guid RequesterUserId { get; set; }

    /// <summary>User assigned to handle the ticket.</summary>
    public Guid? AssignedUserId { get; set; }

    /// <summary>Group assigned to handle the ticket.</summary>
    public Guid? AssignedGroupId { get; set; }

    /// <summary>ITIL category.</summary>
    public Guid? ItilCategoryId { get; set; }

    /// <summary>SLA level (first response, resolution).</summary>
    public Guid? SlaLevelId { get; set; }

    /// <summary>Associated asset (optional).</summary>
    public Guid? AssetId { get; set; }

    /// <summary>Location where the issue occurred.</summary>
    public Guid? LocationId { get; set; }

    /// <summary>Source of the ticket (phone, email, web, helpdesk).</summary>
    public TicketSource Source { get; set; } = TicketSource.Helpdesk;

    /// <summary>Validation status for changes.</summary>
    public TicketValidationStatus ValidationStatus { get; set; } = TicketValidationStatus.None;

    /// <summary>Satisfaction rating (1-5, null if not rated).</summary>
    public int? SatisfactionRating { get; set; }

    /// <summary>User comments on satisfaction.</summary>
    public string? SatisfactionComment { get; set; }

    // Navigation properties
    public ICollection<TicketTask> Tasks { get; set; } = new List<TicketTask>();
    public ICollection<TicketCost> Costs { get; set; } = new List<TicketCost>();
    public ICollection<TicketHistory> History { get; set; } = new List<TicketHistory>();
    public ICollection<TicketValidation> Validations { get; set; } = new List<TicketValidation>();
}

public enum TicketType
{
    Incident = 1,
    Request = 2,
    Problem = 3,
    Change = 4
}

public enum TicketStatus
{
    New = 1,
    Assigned = 2,
    InProgress = 3,
    WaitingForUser = 4,
    WaitingForSupplier = 5,
    Solved = 6,
    Closed = 7,
    Cancelled = 8
}

public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    VeryHigh = 4,
    Critical = 5
}

public enum TicketImpact
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum TicketUrgency
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum TicketSource
{
    Helpdesk = 1,
    Phone = 2,
    Email = 3,
    Web = 4,
    Sms = 5,
    Api = 6
}

public enum TicketValidationStatus
{
    None = 1,
    WaitingApproval = 2,
    Approved = 3,
    Rejected = 4
}
