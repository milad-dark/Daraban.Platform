using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Daraban.Modules.Assets.Data.Configurations;

public class AssetStatusHistoryConfiguration : IEntityTypeConfiguration<AssetStatusHistory>
{
    public void Configure(EntityTypeBuilder<AssetStatusHistory> builder)
    {
        builder.ToTable("asset_status_history", "assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FromStatus).HasConversion<string>();
        builder.Property(x => x.ToStatus).HasConversion<string>();
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasOne(x => x.Asset)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
