using Daraban.Modules.Identity.Data.Entities;

namespace Daraban.Modules.Identity.Data.Repositories;

public interface IRefreshTokenRepository
{
    /// <summary>Looked up by hash -- callers never have a raw token to search by except the
    /// one just presented to them, which they hash before calling this.</summary>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>Reuse-detection response (Task 1.3 SS3): revokes every token in the family,
    /// not just the one presented, since a replayed token means the whole chain is compromised.</summary>
    Task RevokeFamilyAsync(Guid familyId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
