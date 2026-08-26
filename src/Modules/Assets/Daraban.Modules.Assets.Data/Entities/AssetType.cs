namespace Daraban.Modules.Assets.Data.Entities;

public class AssetType
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public AssetCategory Category { get; set; } = null!;
    public ICollection<AssetField> Fields { get; set; } = new List<AssetField>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
