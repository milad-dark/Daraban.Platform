namespace Daraban.Modules.Assets.Data.Entities;

public enum AssignmentTargetType
{
    User = 0,
    Department = 1,
    Location = 2
}
public class AssetAssignment
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public AssignmentTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string? TargetName { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public DateTimeOffset? UnassignedAt { get; set; }
    public Guid AssignedByUserId { get; set; }
    public string? Notes { get; set; }
    public bool IsCurrent { get; set; } = true;
    public Asset Asset { get; set; } = null!;
}
