using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Daraban.Modules.Assets.Data.Configurations;

public class AssetFieldValueConfiguration : IEntityTypeConfiguration<AssetFieldValue>
{
    public void Configure(EntityTypeBuilder<AssetFieldValue> builder)
    {
        builder.ToTable("asset_field_values", "assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Value).HasMaxLength(2000);
        builder.HasIndex(x => new { x.AssetId, x.AssetFieldId }).IsUnique();
        builder.HasOne(x => x.Asset)
            .WithMany(x => x.FieldValues)
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.AssetField)
            .WithMany(x => x.Values)
            .HasForeignKey(x => x.AssetFieldId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
