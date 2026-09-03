using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Data.Entities;

/// <summary>
/// Budget tracking entity — represents a budget allocation for a specific period.
/// Follows GLPI's budget model with spent visualization and entity assignment.
/// </summary>
public class Budget : TenantScopedEntity
{
    /// <summary>Budget name (e.g., "IT Hardware 2026").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique reference code for the budget.</summary>
    public string? Reference { get; set; }

    /// <summary>Budget amount in the configured currency.</summary>
    public decimal Amount { get; set; }

    /// <summary>Amount already spent from this budget.</summary>
    public decimal Spent { get; set; }

    /// <summary>Budget start date.</summary>
    public DateTimeOffset StartDate { get; set; }

    /// <summary>Budget end date.</summary>
    public DateTimeOffset EndDate { get; set; }

    /// <summary>Budget location (optional).</summary>
    public Guid? LocationId { get; set; }

    /// <summary>Budget comment/notes.</summary>
    public string? Comment { get; set; }

    /// <summary>Whether this budget is currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional link to a parent budget for hierarchical tracking.</summary>
    public Guid? ParentBudgetId { get; set; }

    /// <summary>Calculated remaining amount (Amount - Spent).</summary>
    public decimal Remaining => Amount - Spent;

    /// <summary>Calculated percentage used.</summary>
    public decimal PercentUsed => Amount > 0 ? (Spent / Amount) * 100 : 0;

    // Navigation properties
    public Budget? ParentBudget { get; set; }
    public ICollection<Budget> ChildBudgets { get; set; } = new List<Budget>();
    public ICollection<Infocom> InfocomEntries { get; set; } = new List<Infocom>();
    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
}
