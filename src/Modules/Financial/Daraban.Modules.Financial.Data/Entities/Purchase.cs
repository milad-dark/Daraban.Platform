using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Data.Entities;

/// <summary>
/// Purchase entity — tracks purchase orders and their financial details.
/// Links purchases to suppliers, budgets, and assets.
/// </summary>
public class Purchase : TenantScopedEntity
{
    /// <summary>Purchase order number (unique).</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>Purchase order name/description.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Purchase status.</summary>
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Draft;

    /// <summary>Associated supplier/vendor.</summary>
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>Budget used for this purchase.</summary>
    public Guid? BudgetId { get; set; }
    public Budget? Budget { get; set; }

    /// <summary>Date the purchase was requested.</summary>
    public DateTimeOffset RequestedDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Date the purchase was approved.</summary>
    public DateTimeOffset? ApprovedDate { get; set; }

    /// <summary>User who requested the purchase.</summary>
    public Guid RequestedById { get; set; }

    /// <summary>User who approved the purchase.</summary>
    public Guid? ApprovedById { get; set; }

    /// <summary>Date the purchase was ordered.</summary>
    public DateTimeOffset? OrderedDate { get; set; }

    /// <summary>Expected delivery date.</summary>
    public DateTimeOffset? ExpectedDeliveryDate { get; set; }

    /// <summary>Actual delivery date.</summary>
    public DateTimeOffset? ReceivedDate { get; set; }

    /// <summary>Total purchase amount.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Tax amount.</summary>
    public decimal TaxAmount { get; set; } = 0;

    /// <summary>Total including tax.</summary>
    public decimal TotalWithTax => TotalAmount + TaxAmount;

    /// <summary>Currency code (ISO 4217).</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Exchange rate to base currency (if foreign currency).</summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>Payment terms (e.g., "Net 30").</summary>
    public string? PaymentTerms { get; set; }

    /// <summary>Payment method.</summary>
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>Date payment was made.</summary>
    public DateTimeOffset? PaymentDate { get; set; }

    /// <summary>Whether this purchase has been paid.</summary>
    public bool IsPaid { get; set; } = false;

    /// <summary>Delivery address.</summary>
    public string? DeliveryAddress { get; set; }

    /// <summary>Comments/notes about the purchase.</summary>
    public string? Comment { get; set; }

    /// <summary>Supplier's quote reference number.</summary>
    public string? SupplierQuoteReference { get; set; }

    // Navigation properties
    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}

/// <summary>
/// Individual line items in a purchase order.
/// </summary>
public class PurchaseItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PurchaseId { get; set; }
    public Purchase Purchase { get; set; } = null!;

    /// <summary>Item description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Item reference/SKU.</summary>
    public string? ItemReference { get; set; }

    /// <summary>Quantity ordered.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Unit price.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Discount percentage (0-100).</summary>
    public decimal DiscountPercent { get; set; } = 0;

    /// <summary>Line total (Quantity × UnitPrice × (1 - DiscountPercent/100)).</summary>
    public decimal LineTotal => Quantity * UnitPrice * (1 - DiscountPercent / 100);

    /// <summary>Tax rate percentage.</summary>
    public decimal TaxRate { get; set; } = 0;

    /// <summary>Tax amount for this line.</summary>
    public decimal TaxAmount => LineTotal * (TaxRate / 100);

    /// <summary>Total including tax.</summary>
    public decimal TotalWithTax => LineTotal + TaxAmount;

    /// <summary>Associated asset (if this item creates an asset).</summary>
    public Guid? AssetId { get; set; }

    /// <summary>Comments about this line item.</summary>
    public string? Comment { get; set; }
}

public enum PurchaseStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Ordered = 4,
    PartiallyReceived = 5,
    Received = 6,
    Cancelled = 7
}

public enum PaymentMethod
{
    Cash = 1,
    Check = 2,
    CreditCard = 3,
    BankTransfer = 4,
    PurchaseOrder = 5,
    Other = 6
}
