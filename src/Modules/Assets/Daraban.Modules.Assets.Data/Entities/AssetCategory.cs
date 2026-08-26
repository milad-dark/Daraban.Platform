namespace Daraban.Modules.Assets.Data.Entities;

public class AssetCategory
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public AssetCategory? Parent { get; set; }
    public ICollection<AssetCategory> Children { get; set; } = new List<AssetCategory>();
    public ICollection<AssetType> AssetTypes { get; set; } = new List<AssetType>();
}
