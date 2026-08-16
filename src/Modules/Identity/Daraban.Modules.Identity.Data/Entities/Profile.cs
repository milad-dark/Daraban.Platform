namespace Daraban.Modules.Identity.Data.Entities;

/// <summary>A "role" (Task 1.3 SS4.1: role in this system IS Profile -- named bundle of
/// granular rights via ProfileRight). 'Super-Admin', 'Technician', 'Self-Service', etc.</summary>
public class Profile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public bool IsDefault { get; set; }
}
