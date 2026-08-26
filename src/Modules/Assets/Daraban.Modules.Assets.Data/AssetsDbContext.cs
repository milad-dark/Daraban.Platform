using Daraban.Modules.Assets.Data.Configurations;
using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Assets.Data;

public class AssetsDbContext : DbContext
{
    public AssetsDbContext(DbContextOptions<AssetsDbContext> options) : base(options) { }

    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetType> AssetTypes => Set<AssetType>();
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<AssetField> AssetFields => Set<AssetField>();
    public DbSet<AssetFieldValue> AssetFieldValues => Set<AssetFieldValue>();
    public DbSet<AssetModel> AssetModels => Set<AssetModel>();
    public DbSet<AssetRelationship> AssetRelationships => Set<AssetRelationship>();
    public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();
    public DbSet<AssetStatusHistory> AssetStatusHistories => Set<AssetStatusHistory>();
    public DbSet<AssetDocument> AssetDocuments => Set<AssetDocument>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();

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
