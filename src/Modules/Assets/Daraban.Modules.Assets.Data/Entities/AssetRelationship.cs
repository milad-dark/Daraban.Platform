namespace Daraban.Modules.Assets.Data.Entities;

public enum AssetRelationshipType
{
    ConnectedTo = 0,
    DockedIn = 1,
    InstalledIn = 2,
    PoweredBy = 3,
    ManagedBy = 4
}
public class AssetRelationship
{
    public Guid Id { get; set; }
    public Guid SourceAssetId { get; set; }
    public Guid TargetAssetId { get; set; }
    public AssetRelationshipType RelationshipType { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Asset SourceAsset { get; set; } = null!;
    public Asset TargetAsset { get; set; } = null!;
}
