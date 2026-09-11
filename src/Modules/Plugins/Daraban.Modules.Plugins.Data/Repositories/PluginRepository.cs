using Daraban.Modules.Plugins.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Plugins.Data.Repositories;

/// <summary>Persistence contract for the plugin registry (Task 7.5).</summary>
public interface IPluginRepository
{
    Task<IReadOnlyList<Plugin>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Non-tombstone rows only -- what the plugin manager UI lists.</summary>
    Task<IReadOnlyList<Plugin>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>Latest registry row for a plugin id, tombstones included (install loop guard).</summary>
    Task<Plugin?> GetByPluginIdAsync(string pluginId, CancellationToken ct = default);

    Task<Plugin?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Plugin plugin, CancellationToken ct = default);

    void Update(Plugin plugin);

    void Remove(Plugin plugin);

    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>EF Core persistence for the plugin registry. Rows are created/updated by the
/// plugin manager only; every mutation path validates state transitions first.</summary>
public class PluginRepository(PluginsDbContext db) : IPluginRepository
{
    public async Task<IReadOnlyList<Plugin>> GetAllAsync(CancellationToken ct = default) =>
        await db.Plugins.AsNoTracking()
            .OrderByDescending(p => p.InstalledAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Plugin>> GetActiveAsync(CancellationToken ct = default) =>
        await db.Plugins.AsNoTracking()
            .Where(p => p.Status != "uninstalled")
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    public Task<Plugin?> GetByPluginIdAsync(string pluginId, CancellationToken ct = default) =>
        db.Plugins
            .Where(p => p.PluginId == pluginId)
            .OrderByDescending(p => p.InstalledAt)
            .FirstOrDefaultAsync(ct);

    public Task<Plugin?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Plugins.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(Plugin plugin, CancellationToken ct = default) =>
        await db.Plugins.AddAsync(plugin, ct);

    public void Update(Plugin plugin) => db.Plugins.Update(plugin);

    public void Remove(Plugin plugin) => db.Plugins.Remove(plugin);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
