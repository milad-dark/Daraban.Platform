namespace Daraban.Modules.Assets.Data.Entities;

public class AssetStatusHistory
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public AssetStatus FromStatus { get; set; }
    public AssetStatus ToStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Asset Asset { get; set; } = null!;
}
