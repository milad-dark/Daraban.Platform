using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Financial.Data.Configurations;

public class ContractAssetConfiguration : IEntityTypeConfiguration<ContractAsset>
{
    public void Configure(EntityTypeBuilder<ContractAsset> builder)
    {
        builder.ToTable("contract_assets");

        builder.HasKey(ca => new { ca.ContractId, ca.AssetId });

        builder.Property(ca => ca.ContractId)
            .HasColumnName("contract_id");

        builder.Property(ca => ca.AssetId)
            .HasColumnName("asset_id");

        builder.Property(ca => ca.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Relationships
        builder.HasOne(ca => ca.Contract)
            .WithMany(c => c.ContractAssets)
            .HasForeignKey(ca => ca.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(ca => ca.AssetId)
            .HasDatabaseName("ix_contract_assets_asset_id");
    }
}
