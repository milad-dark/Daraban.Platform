using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Reporting.Data;

/// <summary>Owns the "reporting" PostgreSQL schema (Task 1.2). Each module owns its own
/// DbContext/migrations -- no shared god-context across modules.</summary>
public class ReportingDbContext : DbContext
{
    public ReportingDbContext(DbContextOptions<ReportingDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("reporting");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
