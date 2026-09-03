using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Data.Entities;

/// <summary>
/// Contract entity — tracks maintenance, rental, lease, and service agreements.
/// Follows GLPI's contract model with duration, cost, and asset associations.
/// </summary>
public class Contract : TenantScopedEntity
{
    /// <summary>Contract name/title.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique contract reference number.</summary>
    public string? Reference { get; set; }

    /// <summary>Contract type (Maintenance, Lease, Rental, etc.).</summary>
    public Guid? ContractTypeId { get; set; }

    /// <summary>Associated supplier/vendor.</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>Contract start date.</summary>
    public DateTimeOffset StartDate { get; set; }

    /// <summary>Contract end date.</summary>
    public DateTimeOffset? EndDate { get; set; }

    /// <summary>Duration in months (auto-calculated if EndDate is set).</summary>
    public int? DurationMonths { get; set; }

    /// <summary>Contract value/total cost.</summary>
    public decimal? Value { get; set; }

    /// <summary>Monthly cost.</summary>
    public decimal? MonthlyCost { get; set; }

    /// <summary>Cost per year (annual).</summary>
    public decimal? AnnualCost { get; set; }

    /// <summary>Currency code (ISO 4217, e.g., "USD", "EUR").</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Billing frequency.</summary>
    public BillingFrequency BillingFrequency { get; set; } = BillingFrequency.Monthly;

    /// <summary>Contract status.</summary>
    public ContractStatus Status { get; set; } = ContractStatus.Active;

    /// <summary>Auto-renewal flag.</summary>
    public bool AutoRenew { get; set; } = false;

    /// <summary>Notice period in days before expiry.</summary>
    public int? NoticePeriodDays { get; set; }

    /// <summary>When the contract was signed.</summary>
    public DateTimeOffset? SignedDate { get; set; }

    /// <summary>User who signed the contract.</summary>
    public Guid? SignedById { get; set; }

    /// <summary>Physical location where contract documents are stored.</summary>
    public string? DocumentLocation { get; set; }

    /// <summary>Contract terms and conditions notes.</summary>
    public string? Terms { get; set; }

    /// <summary>Internal comments.</summary>
    public string? Comment { get; set; }

    /// <summary>Is this a critical contract requiring special attention.</summary>
    public bool IsCritical { get; set; } = false;

    // Navigation properties
    public ContractType? ContractType { get; set; }
    public Supplier? Supplier { get; set; }
    public ICollection<ContractAsset> ContractAssets { get; set; } = new List<ContractAsset>();
    public ICollection<ContractCost> ContractCosts { get; set; } = new List<ContractCost>();
}

/// <summary>
/// Links contracts to assets (many-to-many).
/// </summary>
public class ContractAsset
{
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    public Guid AssetId { get; set; }
    // public Asset Asset { get; set; } = null!; // Reference to Assets module

    /// <summary>When this association was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Cost entries for contracts (supports multiple billing periods).
/// </summary>
public class ContractCost
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    /// <summary>Cost amount for this period.</summary>
    public decimal Amount { get; set; }

    /// <summary>Billing period start.</summary>
    public DateTimeOffset PeriodStart { get; set; }

    /// <summary>Billing period end.</summary>
    public DateTimeOffset PeriodEnd { get; set; }

    /// <summary>Invoice reference number.</summary>
    public string? InvoiceReference { get; set; }

    /// <summary>Date the invoice was received.</summary>
    public DateTimeOffset? InvoiceDate { get; set; }

    /// <summary>Whether this cost has been paid.</summary>
    public bool IsPaid { get; set; } = false;

    /// <summary>Date the cost was paid.</summary>
    public DateTimeOffset? PaidDate { get; set; }

    /// <summary>Comments about this cost entry.</summary>
    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum BillingFrequency
{
    Monthly = 1,
    Quarterly = 2,
    SemiAnnual = 3,
    Annual = 4,
    OneTime = 5
}

public enum ContractStatus
{
    Draft = 1,
    Active = 2,
    Suspended = 3,
    Expired = 4,
    Cancelled = 5,
    Terminated = 6
}
