using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Daraban.Modules.Assets.Data.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("assets", "assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(300);
        builder.Property(x => x.AssetTag).HasMaxLength(100);
        builder.Property(x => x.SerialNumber).HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<string>();
        builder.Property(x => x.PurchaseCost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.PurchaseCurrency).HasMaxLength(3);
        builder.Property(x => x.OrderNumber).HasMaxLength(100);
        builder.Property(x => x.SupplierName).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasQueryFilter(x => x.DeletedAt == null);
        builder.HasIndex(x => x.AssetTag);
        builder.HasIndex(x => x.SerialNumber);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.EntityNodeId);
        builder.HasOne(x => x.AssetType)
            .WithMany(x => x.Assets)
            .HasForeignKey(x => x.AssetTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssetModel)
            .WithMany(x => x.Assets)
            .HasForeignKey(x => x.AssetModelId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Location)
            .WithMany(x => x.Assets)
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
