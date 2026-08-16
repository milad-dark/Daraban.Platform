namespace Daraban.Modules.Identity.Data.Entities;

/// <summary>
/// The recursive entity/tenant tree (Task 1.2 SS3, Task 1.3 SS4.3). Named EntityNode, not
/// Entity -- "Entity" collides badly with the general EF Core/DDD term for "a row with an
/// Id," and this is a specific business concept (an org/tenant node), not a generic base
/// type. Table itself is still identity.entities per the DB design.
/// </summary>
public class EntityNode
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>Materialized path (e.g. "/root-id/child-id/grandchild-id/"), maintained on
    /// write -- lets recursive-scope queries (Task 1.2 SS9, Task 1.3 SS4.3) use a cheap
    /// indexed prefix match instead of a recursive CTE on every permission check.</summary>
    public string FullPath { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
