namespace Daraban.Modules.Assets.Data.Entities;

public class Manufacturer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? SupportUrl { get; set; }
    public string? SupportPhone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public ICollection<AssetModel> Models { get; set; } = new List<AssetModel>();
}
