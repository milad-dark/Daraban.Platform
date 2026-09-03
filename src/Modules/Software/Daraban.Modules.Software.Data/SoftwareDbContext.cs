using Daraban.Modules.Software.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Software.Data;

/// <summary>Owns the "software" PostgreSQL schema. Each module owns its own
/// DbContext/migrations -- no shared god-context across modules.</summary>
public class SoftwareDbContext : DbContext
{
    public SoftwareDbContext(DbContextOptions<SoftwareDbContext> options) : base(options) { }

    // Software entities
    public DbSet<Software> Softwares => Set<Software>();
    public DbSet<SoftwareLicense> Licenses => Set<SoftwareLicense>();
    public DbSet<SoftwareInstallation> Installations => Set<SoftwareInstallation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("software");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SoftwareDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
