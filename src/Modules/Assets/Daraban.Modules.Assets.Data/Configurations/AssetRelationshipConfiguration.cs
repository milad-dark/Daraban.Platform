using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Daraban.Modules.Assets.Data.Configurations;

public class AssetRelationshipConfiguration : IEntityTypeConfiguration<AssetRelationship>
{
    public void Configure(EntityTypeBuilder<AssetRelationship> builder)
    {
        builder.ToTable("asset_relationships", "assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RelationshipType).HasConversion<string>();
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasOne(x => x.SourceAsset)
            .WithMany(x => x.RelationshipsAsSource)
            .HasForeignKey(x => x.SourceAssetId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.TargetAsset)
            .WithMany(x => x.RelationshipsAsTarget)
            .HasForeignKey(x => x.TargetAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
