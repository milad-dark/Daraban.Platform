using Daraban.Modules.Identity.Data.Entities;

namespace Daraban.Modules.Identity.Data.Repositories;

/// <summary>Interface lives here (Services), implementation lives in Data -- purely so
/// UserService can be unit-tested with a fake/mock, not a DDD port/adapter concept
/// (Task 1.1 SS2.2).</summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<User> Items, int TotalCount)> SearchAsync(
        Guid? entityId, string? q, int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
