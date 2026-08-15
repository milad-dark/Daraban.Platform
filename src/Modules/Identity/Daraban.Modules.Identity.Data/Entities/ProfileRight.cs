namespace Daraban.Modules.Identity.Data.Entities;

/// <summary>One granular right within a Profile. The permission string a policy checks
/// against (Task 1.3 SS4.2) is Module + "." + Action, e.g. Module="assets" Action="write",
/// or Module="servicedesk" Action="tickets.read.own" for the finer own/group/all
/// distinction GLPI's own model draws in a few specific places.</summary>
public class ProfileRight
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string Module { get; set; } = default!;
    public string Action { get; set; } = default!;

    /// <summary>Whether this right, when granted via a recursive UserProfileEntity, also
    /// applies to descendant entities. Distinct from UserProfileEntity.IsRecursive (Task 1.2
    /// keeps both -- a right can be inherently org-wide-safe or not, independent of whether
    /// the *grant* itself was made recursively).</summary>
    public bool IsRecursive { get; set; }
}
