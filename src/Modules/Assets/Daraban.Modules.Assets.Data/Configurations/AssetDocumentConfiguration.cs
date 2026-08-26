using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Daraban.Modules.Assets.Data.Configurations;

public class AssetDocumentConfiguration : IEntityTypeConfiguration<AssetDocument>
{
    public void Configure(EntityTypeBuilder<AssetDocument> builder)
    {
        builder.ToTable("asset_documents", "assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
        builder.Property(x => x.FilePath).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.FileType).IsRequired().HasMaxLength(100);
        builder.HasOne(x => x.Asset)
            .WithMany(x => x.Documents)
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
