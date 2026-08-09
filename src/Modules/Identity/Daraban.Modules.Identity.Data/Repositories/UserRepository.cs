using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Identity.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _db;
    public UserRepository(IdentityDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByUsernameOrEmailAsync(string usernameOrEmail, CancellationToken ct = default)
    {
        var normalized = usernameOrEmail.Trim().ToLowerInvariant();
        return _db.Users.FirstOrDefaultAsync(
            u => u.Username.ToLower() == normalized || u.Email.ToLower() == normalized, ct);
    }

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        return _db.Users.AnyAsync(u => u.Username.ToLower() == normalized, ct);
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _db.Users.AnyAsync(u => u.Email.ToLower() == normalized, ct);
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> SearchAsync(
        Guid? entityId, string? q, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Users.AsQueryable();
        if (entityId is not null) query = query.Where(u => u.DefaultEntityId == entityId);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(u => u.DisplayName.Contains(q) || u.Email.Contains(q));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(u => u.DisplayName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task AddAsync(User user, CancellationToken ct = default) => await _db.Users.AddAsync(user, ct);
    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
