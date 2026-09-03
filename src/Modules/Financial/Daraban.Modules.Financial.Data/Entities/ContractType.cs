using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Data.Entities;

/// <summary>
/// Contract type classification — categorizes contracts by their nature.
/// </summary>
public class ContractType : TenantScopedEntity
{
    /// <summary>Type name (e.g., "Maintenance", "Lease", "Rental").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Type description.</summary>
    public string? Description { get; set; }

    /// <summary>Default duration in months for contracts of this type.</summary>
    public int? DefaultDurationMonths { get; set; }

    /// <summary>Whether this type is currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Sort order for display.</summary>
    public int SortOrder { get; set; } = 0;

    // Navigation properties
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
