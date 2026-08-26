namespace Daraban.Modules.Assets.Data.Entities;

public class Asset
{
    public Guid Id { get; set; }
    public Guid AssetTypeId { get; set; }
    public Guid? AssetModelId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid EntityNodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AssetTag { get; set; }
    public string? SerialNumber { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.InStock;
    public DateOnly? PurchaseDate { get; set; }
    public decimal? PurchaseCost { get; set; }
    public string? PurchaseCurrency { get; set; }
    public string? OrderNumber { get; set; }
    public string? SupplierName { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public AssetType AssetType { get; set; } = null!;
    public AssetModel? AssetModel { get; set; }
    public Location? Location { get; set; }
    public ICollection<AssetFieldValue> FieldValues { get; set; } = new List<AssetFieldValue>();
    public ICollection<AssetRelationship> RelationshipsAsSource { get; set; } = new List<AssetRelationship>();
    public ICollection<AssetRelationship> RelationshipsAsTarget { get; set; } = new List<AssetRelationship>();
    public ICollection<AssetAssignment> Assignments { get; set; } = new List<AssetAssignment>();
    public ICollection<AssetStatusHistory> StatusHistory { get; set; } = new List<AssetStatusHistory>();
    public ICollection<AssetDocument> Documents { get; set; } = new List<AssetDocument>();
}
