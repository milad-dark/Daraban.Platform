using Daraban.Modules.Identity.Data.Entities;

namespace Daraban.Modules.Identity.Data.Repositories;

/// <summary>
/// Lives in Data (next to its implementation, UserRepository), NOT in Services.
/// Services already references Data for the DbContext/entities, so having Data also
/// reference Services just for this interface would be a circular project reference.
/// Keeping the interface here is a one-line relocation, not a design change: UserService
/// still depends on the interface, not the concrete UserRepository, so it's still mockable
/// in unit tests via Moq.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Case-insensitive match on username OR email -- used by login, which accepts
    /// either. Deliberately a single method rather than two, so callers can't accidentally
    /// leak which field matched via different code paths/timing.</summary>
    Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken ct = default);

    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);

    Task<(IReadOnlyList<User> Items, int TotalCount)> SearchAsync(
        Guid? entityId, string? q, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
