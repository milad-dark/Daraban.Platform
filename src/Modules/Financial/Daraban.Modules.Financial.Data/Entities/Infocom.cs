using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Data.Entities;

/// <summary>
/// Infocom entity — stores financial information for each asset.
/// Tracks purchase cost, depreciation, value over time, and supplier relationships.
/// Follows GLPI's infocom model with IME (Immo) and OPEX tracking.
/// </summary>
public class Infocom : TenantScopedEntity
{
    /// <summary>Associated asset ID.</summary>
    public Guid AssetId { get; set; }

    // public Asset Asset { get; set; } = null!; // Reference to Assets module

    /// <summary>Purchase order number.</summary>
    public string? PurchaseOrderNumber { get; set; }

    /// <summary>Invoice reference number.</summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>Date of purchase.</summary>
    public DateTimeOffset? PurchaseDate { get; set; }

    /// <summary>Date the asset was delivered.</summary>
    public DateTimeOffset? DeliveryDate { get; set; }

    /// <summary>Date the asset was put into service.</summary>
    public DateTimeOffset? UseDate { get; set; }

    /// <summary>Purchase price/cost.</summary>
    public decimal PurchaseCost { get; set; }

    /// <summary>Additional costs (shipping, installation, etc.).</summary>
    public decimal AdditionalCost { get; set; }

    /// <summary>Total investment cost (PurchaseCost + AdditionalCost).</summary>
    public decimal TotalCost => PurchaseCost + AdditionalCost;

    /// <summary>Currency code (ISO 4217).</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Supplier who sold the asset.</summary>
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>Budget used for this purchase.</summary>
    public Guid? BudgetId { get; set; }
    public Budget? Budget { get; set; }

    /// <summary>Depreciation method.</summary>
    public DepreciationMethod DepreciationMethod { get; set; } = DepreciationMethod.StraightLine;

    /// <summary>Depreciation duration in months.</summary>
    public int DepreciationDurationMonths { get; set; } = 36;

    /// <summary>Depreciation coefficient (for declining balance method).</summary>
    public decimal? DepreciationCoefficient { get; set; }

    /// <summary>Whether depreciation starts on purchase or use date.</summary>
    public bool DepreciationOnUseDate { get; set; } = true;

    /// <summary>Current depreciated value.</summary>
    public decimal CurrentValue { get; set; }

    /// <summary>Residual value at end of depreciation.</summary>
    public decimal ResidualValue { get; set; } = 0;

    /// <summary>Warranty start date.</summary>
    public DateTimeOffset? WarrantyStartDate { get; set; }

    /// <summary>Warranty end date.</summary>
    public DateTimeOffset? WarrantyEndDate { get; set; }

    /// <summary>Warranty details/terms.</summary>
    public string? WarrantyDetails { get; set; }

    /// <summary>Insurance start date.</summary>
    public DateTimeOffset? InsuranceStartDate { get; set; }

    /// <summary>Insurance end date.</summary>
    public DateTimeOffset? InsuranceEndDate { get; set; }

    /// <summary>Insurance value.</summary>
    public decimal? InsuranceValue { get; set; }

    /// <summary>Comments about the financial information.</summary>
    public string? Comment { get; set; }

    /// <summary>Date the asset was decommissioned/sold.</summary>
    public DateTimeOffset? DecommissionDate { get; set; }

    /// <summary>Sale price if asset was sold.</summary>
    public decimal? SalePrice { get; set; }

    /// <summary>Whether this infocom entry is still active.</summary>
    public bool IsActive { get; set; } = true;
}

public enum DepreciationMethod
{
    /// <summary>Linear depreciation (equal amounts each period).</summary>
    StraightLine = 1,

    /// <summary>Accelerated depreciation (higher amounts early).</summary>
    DecliningBalance = 2,

    /// <summary>Sum of years digits.</summary>
    SumOfYearsDigits = 3,

    /// <summary>No depreciation tracking.</summary>
    None = 4
}
