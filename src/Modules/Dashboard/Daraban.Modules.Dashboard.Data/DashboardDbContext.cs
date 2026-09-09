using Daraban.Modules.Dashboard.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Dashboard.Data;

/// <summary>Owns the "dashboard" PostgreSQL schema (Task 7.1). Each module owns its own
/// DbContext/migrations -- no shared god-context across modules.</summary>
public class DashboardDbContext : DbContext
{
    public DashboardDbContext(DbContextOptions<DashboardDbContext> options) : base(options) { }

    public DbSet<DashboardLayout> Layouts => Set<DashboardLayout>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Default schema kept for consistency; the configuration pins the table explicitly.
        modelBuilder.HasDefaultSchema("dashboard");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DashboardDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
