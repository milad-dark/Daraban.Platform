using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Identity.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _db;
    public UserRepository(IdentityDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    // EF.Functions.ILike maps to Postgres ILIKE, which is index-friendly and does the
    // case-insensitive comparison in the database. The previous `u.Username.ToLower() == x` shape
    // wrapped the column in lower(), which no plain index on username can serve.
    public Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken ct = default)
    {
        var normalized = usernameOrEmail.Trim();
        return _db.Users.FirstOrDefaultAsync(
            u => EF.Functions.ILike(u.Username, normalized) || EF.Functions.ILike(u.Email, normalized), ct);
    }

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = username.Trim();
        return _db.Users.AnyAsync(u => EF.Functions.ILike(u.Username, normalized), ct);
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim();
        return _db.Users.AnyAsync(u => EF.Functions.ILike(u.Email, normalized), ct);
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> SearchAsync(
        Guid? entityId, string? q, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Users.AsNoTracking().AsQueryable();
        if (entityId is not null) query = query.Where(u => u.DefaultEntityId == entityId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            // ILike, not Contains: a case-sensitive substring match meant searching "alice" never
            // found "Alice". Username is included too -- searching by login name is the obvious
            // thing an admin tries first.
            var pattern = $"%{q.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.DisplayName, pattern)
                || EF.Functions.ILike(u.Email, pattern)
                || EF.Functions.ILike(u.Username, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.DisplayName)
            // Tie-break on a unique column: ordering by DisplayName alone is not deterministic when
            // names repeat, so the same row could appear on two pages or on none.
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task AddAsync(User user, CancellationToken ct = default) => await _db.Users.AddAsync(user, ct);
    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
