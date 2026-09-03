using Daraban.Platform.Common;

namespace Daraban.Modules.Software.Data.Entities;

/// <summary>
/// Software license entity — tracks license types, counts, and usage.
/// Follows GLPI's license model with OEM, retail, and volume licensing.
/// </summary>
public class SoftwareLicense : TenantScopedEntity
{
    /// <summary>Associated software.</summary>
    public Guid SoftwareId { get; set; }
    public Software Software { get; set; } = null!;

    /// <summary>License name/title.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>License key/serial number.</summary>
    public string? LicenseKey { get; set; }

    /// <summary>License type.</summary>
    public LicenseType Type { get; set; } = LicenseType.OEM;

    /// <summary>Number of licenses purchased.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Number of licenses currently in use.</summary>
    public int UsedQuantity { get; set; } = 0;

    /// <summary>Number of available licenses (Quantity - UsedQuantity).</summary>
    public int AvailableQuantity => Quantity - UsedQuantity;

    /// <summary>Whether there are enough licenses available.</summary>
    public bool IsCompliant => AvailableQuantity >= 0;

    /// <summary>License purchase date.</summary>
    public DateTimeOffset? PurchaseDate { get; set; }

    /// <summary>License expiration date.</summary>
    public DateTimeOffset? ExpirationDate { get; set; }

    /// <summary>Whether the license auto-renews.</summary>
    public bool AutoRenew { get; set; } = false;

    /// <summary>Purchase cost.</summary>
    public decimal? PurchaseCost { get; set; }

    /// <summary>Currency code (ISO 4217).</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Supplier who sold the license.</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>Contract associated with this license.</summary>
    public Guid? ContractId { get; set; }

    /// <summary>Is this license currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Comments about the license.</summary>
    public string? Comment { get; set; }

    /// <summary>Is the license expired.</summary>
    public bool IsExpired => ExpirationDate.HasValue && ExpirationDate.Value < DateTimeOffset.UtcNow;

    // Navigation properties
    public ICollection<SoftwareInstallation> Installations { get; set; } = new List<SoftwareInstallation>();
}

public enum LicenseType
{
    /// <summary>Original Equipment Manufacturer license (pre-installed).</summary>
    OEM = 1,

    /// <summary>Retail license (purchased separately).</summary>
    Retail = 2,

    /// <summary>Volume license (multiple seats).</summary>
    Volume = 3,

    /// <summary>Software as a Service (subscription).</summary>
    SaaS = 4,

    /// <summary>Open source license.</summary>
    OpenSource = 5,

    /// <summary>Freeware (no license required).</summary>
    Freeware = 6,

    /// <summary>Trial license.</summary>
    Trial = 7,

    /// <summary>Education license.</summary>
    Education = 8,

    /// <summary>Other license type.</summary>
    Other = 9
}
