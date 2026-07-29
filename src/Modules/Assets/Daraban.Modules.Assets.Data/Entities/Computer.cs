using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Data.Entities;

public class Computer : TenantScopedEntity
{
    public string Name { get; set; } = default!;
    public Guid? LocationId { get; set; }
    public string? SerialNumber { get; set; }
    public string? InventoryNumber { get; set; }
    public Guid? ManufacturerId { get; set; }
    public Guid? ModelId { get; set; }
    public Guid? TypeId { get; set; }
    public Guid StateId { get; set; }
    public string? OsName { get; set; }
    public string? OsVersion { get; set; }
    public DateTimeOffset? LastInventoryAt { get; set; }
}
