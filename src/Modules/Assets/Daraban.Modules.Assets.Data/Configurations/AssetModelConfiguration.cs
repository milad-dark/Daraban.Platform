using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Daraban.Modules.Assets.Data.Configurations;

public class AssetModelConfiguration : IEntityTypeConfiguration<AssetModel>
{
    public void Configure(EntityTypeBuilder<AssetModel> builder)
    {
        builder.ToTable("asset_models", "assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ModelNumber).HasMaxLength(100);
        builder.HasQueryFilter(x => x.DeletedAt == null);
        builder.HasOne(x => x.Manufacturer)
            .WithMany(x => x.Models)
            .HasForeignKey(x => x.ManufacturerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssetType)
            .WithMany()
            .HasForeignKey(x => x.AssetTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
