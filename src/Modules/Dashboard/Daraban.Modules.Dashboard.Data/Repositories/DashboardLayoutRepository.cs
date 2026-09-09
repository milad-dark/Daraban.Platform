using Daraban.Modules.Dashboard.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Dashboard.Data.Repositories;

/// <summary>EF Core implementation of layout persistence (Task 7.1).</summary>
public class DashboardLayoutRepository(DashboardDbContext db) : IDashboardLayoutRepository
{
    public Task<DashboardLayout?> GetAsync(Guid userId, string name, CancellationToken ct = default) =>
        db.Layouts.AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == userId && l.Name == name, ct);

    /// <summary>Insert-or-update keyed on (userId, name). Uses a tracked re-fetch so the
    /// unique index conflict surfaces as an update rather than a DbUpdateException.</summary>
    public async Task UpsertAsync(DashboardLayout layout, CancellationToken ct = default)
    {
        var existing = await db.Layouts
            .FirstOrDefaultAsync(l => l.UserId == layout.UserId && l.Name == layout.Name, ct);

        if (existing is not null)
        {
            existing.LayoutJson = layout.LayoutJson;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedById = layout.UpdatedById;
        }
        else
        {
            await db.Layouts.AddAsync(layout, ct);
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
