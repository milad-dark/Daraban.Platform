namespace Daraban.Modules.Assets.Data.Entities;

public class AssetModel
{
    public Guid Id { get; set; }
    public Guid ManufacturerId { get; set; }
    public Guid AssetTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ModelNumber { get; set; }
    public int? ExpectedLifetimeMonths { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Manufacturer Manufacturer { get; set; } = null!;
    public AssetType AssetType { get; set; } = null!;
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
