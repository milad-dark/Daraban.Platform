using Daraban.Modules.Plugins.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Plugins.Data;

/// <summary>
/// Owns the <c>core.plugins</c> registry table (Task 7.5). Follows the Settings module
/// pattern: the cross-cutting core schema is set per-table in the configuration classes,
/// and the table itself is created by an idempotent startup DDL rather than an EF
/// migration (this module owns no migrations assembly).
/// </summary>
public class PluginsDbContext : DbContext
{
    public PluginsDbContext(DbContextOptions<PluginsDbContext> options) : base(options) { }

    public DbSet<Plugin> Plugins => Set<Plugin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PluginsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
