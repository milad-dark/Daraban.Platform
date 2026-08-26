namespace Daraban.Modules.Assets.Data.Entities;

public enum AssetFieldType
{
    Text = 0,
    Number = 1,
    Date = 2,
    Boolean = 3,
    Dropdown = 4,
    Url = 5
}
public class AssetField
{
    public Guid Id { get; set; }
    public Guid AssetTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public AssetFieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public string? DropdownOptions { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public AssetType AssetType { get; set; } = null!;
    public ICollection<AssetFieldValue> Values { get; set; } = new List<AssetFieldValue>();
}
