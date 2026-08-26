using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Daraban.Modules.Assets.Data.Configurations;

public class AssetAssignmentConfiguration : IEntityTypeConfiguration<AssetAssignment>
{
    public void Configure(EntityTypeBuilder<AssetAssignment> builder)
    {
        builder.ToTable("asset_assignments", "assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TargetType).HasConversion<string>();
        builder.Property(x => x.TargetName).HasMaxLength(300);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.AssetId, x.IsCurrent });
        builder.HasOne(x => x.Asset)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
