namespace Daraban.Modules.Identity.Data.Entities;

/// <summary>
/// The three-way association from Task 1.3 SS4.1/Task 1.2 SS3: a user can hold different
/// profiles in different entities, and multiple profiles in the same entity. This row is
/// "user U holds profile P in entity E, and if IsRecursive, also in every descendant of E."
/// </summary>
public class UserProfileEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProfileId { get; set; }
    public Guid EntityId { get; set; }
    public bool IsRecursive { get; set; }
    public bool IsDefault { get; set; }
}
