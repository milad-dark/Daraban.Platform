using Daraban.Modules.Settings.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Settings.Data;

/// <summary>Owns the "core" additions for platform configuration (Task 7.4). The settings
/// table joins audit_logs in the cross-cutting core schema; see the configuration class
/// for why this deliberately lives outside any single module's schema.</summary>
public class SettingsDbContext : DbContext
{
    public SettingsDbContext(DbContextOptions<SettingsDbContext> options) : base(options) { }

    public DbSet<SystemSetting> Settings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SettingsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
