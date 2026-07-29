using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Financial.Data;

/// <summary>Owns the "financial" PostgreSQL schema (Task 1.2). Each module owns its own
/// DbContext/migrations -- no shared god-context across modules.</summary>
public class FinancialDbContext : DbContext
{
    public FinancialDbContext(DbContextOptions<FinancialDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("financial");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinancialDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
