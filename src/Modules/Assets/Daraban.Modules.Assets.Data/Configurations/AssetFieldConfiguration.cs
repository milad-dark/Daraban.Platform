using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Daraban.Modules.Assets.Data.Configurations;

public class AssetFieldConfiguration : IEntityTypeConfiguration<AssetField>
{
    public void Configure(EntityTypeBuilder<AssetField> builder)
    {
        builder.ToTable("asset_fields", "assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DefaultValue).HasMaxLength(500);
        builder.Property(x => x.DropdownOptions).HasColumnType("jsonb");
        builder.Property(x => x.FieldType).HasConversion<string>();
        builder.HasOne(x => x.AssetType)
            .WithMany(x => x.Fields)
            .HasForeignKey(x => x.AssetTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
