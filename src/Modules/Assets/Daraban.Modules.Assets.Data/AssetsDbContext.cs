using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Assets.Data;

public class AssetsDbContext : DbContext
{
    public AssetsDbContext(DbContextOptions<AssetsDbContext> options) : base(options) { }

    public DbSet<Computer> Computers => Set<Computer>();
    // Monitors, NetworkEquipment, Printers, Phones, Peripherals, Software, ...
    // follow the identical shape as Computer (Task 1.2 SS4) -- add alongside as built out.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("assets");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssetsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
