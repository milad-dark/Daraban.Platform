using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Data.Entities;

/// <summary>
/// Supplier/Vendor entity — manages vendor contacts and relationships.
/// Follows GLPI's supplier model with full contact management.
/// </summary>
public class Supplier : TenantScopedEntity
{
    /// <summary>Supplier/company name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Alternative name or trading name.</summary>
    public string? TradingName { get; set; }

    /// <summary>Primary contact person name.</summary>
    public string? ContactName { get; set; }

    /// <summary>Contact email address.</summary>
    public string? Email { get; set; }

    /// <summary>Contact phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Mobile phone number.</summary>
    public string? Mobile { get; set; }

    /// <summary>Fax number.</summary>
    public string? Fax { get; set; }

    /// <summary>Website URL.</summary>
    public string? Website { get; set; }

    /// <summary>Physical address line 1.</summary>
    public string? AddressLine1 { get; set; }

    /// <summary>Physical address line 2.</summary>
    public string? AddressLine2 { get; set; }

    /// <summary>City.</summary>
    public string? City { get; set; }

    /// <summary>State/Province.</summary>
    public string? State { get; set; }

    /// <summary>Postal/ZIP code.</summary>
    public string? PostalCode { get; set; }

    /// <summary>Country.</summary>
    public string? Country { get; set; }

    /// <summary>Registration/Tax ID number.</summary>
    public string? RegistrationNumber { get; set; }

    /// <summary>VAT/Tax number.</summary>
    public string? VatNumber { get; set; }

    /// <summary>IBAN for bank transfers.</summary>
    public string? Iban { get; set; }

    /// <summary>Bank name.</summary>
    public string? BankName { get; set; }

    /// <summary>Bank sort code.</summary>
    public string? SortCode { get; set; }

    /// <summary>Supplier type (e.g., Hardware, Software, Services).</summary>
    public SupplierType Type { get; set; } = SupplierType.Other;

    /// <summary>Whether this supplier is currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Internal comments about the supplier.</summary>
    public string? Comment { get; set; }

    // Navigation properties
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    public ICollection<Infocom> InfocomEntries { get; set; } = new List<Infocom>();
}

public enum SupplierType
{
    Hardware = 1,
    Software = 2,
    Services = 3,
    Network = 4,
    Other = 5
}
