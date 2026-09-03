using Daraban.Platform.Common;

namespace Daraban.Modules.ServiceDesk.Data.Entities;

/// <summary>
/// Cost associated with a ticket — time, materials, external services.
/// </summary>
public class TicketCost : BaseEntity
{
    /// <summary>Reference to the parent ticket.</summary>
    public Guid TicketId { get; set; }

    /// <summary>Cost type (time, material, external, other).</summary>
    public TicketCostType CostType { get; set; } = TicketCostType.Time;

    /// <summary>Description of the cost item.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Cost amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Currency code (ISO 4217).</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>User who recorded this cost.</summary>
    public Guid UserId { get; set; }

    /// <summary>Date the cost was incurred.</summary>
    public DateTimeOffset IncurredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional reference (invoice number, time sheet entry).</summary>
    public string? Reference { get; set; }

    // Navigation property
    public Ticket Ticket { get; set; } = null!;
}

public enum TicketCostType
{
    Time = 1,
    Material = 2,
    External = 3,
    Other = 4
}
