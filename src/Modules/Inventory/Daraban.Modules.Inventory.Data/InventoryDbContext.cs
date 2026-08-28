using Daraban.Modules.Inventory.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Inventory.Data;

/// <summary>Owns the "inventory" PostgreSQL schema (Task 1.2). Each module owns its own
/// DbContext/migrations -- no shared god-context across modules.</summary>
public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<RawInventorySubmission> RawInventorySubmissions => Set<RawInventorySubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inventory");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
