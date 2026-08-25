namespace Daraban.Modules.Assets.Data.Entities;

public class AssetFieldValue
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Guid AssetFieldId { get; set; }
    public string? Value { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Asset Asset { get; set; } = null!;
    public AssetField AssetField { get; set; } = null!;
}
