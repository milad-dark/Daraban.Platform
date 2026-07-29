using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Automation.Data;

/// <summary>Owns the "automation" PostgreSQL schema (Task 1.2). Each module owns its own
/// DbContext/migrations -- no shared god-context across modules.</summary>
public class AutomationDbContext : DbContext
{
    public AutomationDbContext(DbContextOptions<AutomationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("automation");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AutomationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
