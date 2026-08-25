using Daraban.Modules.Assets.Data.Configurations;
using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Assets.Data;

public class AssetsDbContext : DbContext
{
    public AssetsDbContext(DbContextOptions<AssetsDbContext> options) : base(options) { }

    public DbSet<Computer> Computers => Set<Computer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("assets");
        modelBuilder.ApplyConfiguration(new AssetCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new AssetTypeConfiguration());
        modelBuilder.ApplyConfiguration(new ManufacturerConfiguration());
        modelBuilder.ApplyConfiguration(new AssetModelConfiguration());
        modelBuilder.ApplyConfiguration(new LocationConfiguration());
        modelBuilder.ApplyConfiguration(new AssetFieldConfiguration());
        modelBuilder.ApplyConfiguration(new AssetConfiguration());
        modelBuilder.ApplyConfiguration(new AssetFieldValueConfiguration());
        modelBuilder.ApplyConfiguration(new AssetRelationshipConfiguration());
        modelBuilder.ApplyConfiguration(new AssetAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new AssetStatusHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new AssetDocumentConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
