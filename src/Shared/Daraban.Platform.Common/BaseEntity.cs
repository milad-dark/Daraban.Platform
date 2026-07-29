namespace Daraban.Platform.Common;

/// <summary>
/// Plain base type for every EF Core entity across every module. Deliberately not an
/// "AggregateRoot" -- this project stays out of DDD tactical patterns (Task 1.1).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public Guid? UpdatedById { get; set; }
}

/// <summary>Adds the soft-delete pair used across every tenant-scoped table (Task 1.2 SS1).</summary>
public abstract class SoftDeletableEntity : BaseEntity
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>Adds the entity/tenant scope column present on every tenant-scoped table.</summary>
public abstract class TenantScopedEntity : SoftDeletableEntity
{
    public Guid EntityId { get; set; }
}
