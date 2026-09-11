using Daraban.Modules.Settings.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Settings.Data.Repositories;

/// <summary>Persistence contract for system settings (Task 7.4).</summary>
public interface ISystemSettingRepository
{
    Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Tracked fetch for an update; null when the key is unknown.</summary>
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>EF Core persistence for system settings. Intentionally narrow: rows are
/// seeded by <c>SystemSettingSeeder</c> and only ever updated here -- no Add/Remove
/// surface exists, so unknown keys cannot be minted through this API.</summary>
public class SystemSettingRepository(SettingsDbContext db) : ISystemSettingRepository
{
    public async Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken ct = default) =>
        await db.Settings.AsNoTracking()
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync(ct);

    public Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
