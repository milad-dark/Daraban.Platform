using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Financial.Data.Configurations;

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.ToTable("purchase_items");

        builder.HasKey(pi => pi.Id);

        builder.Property(pi => pi.Id)
            .HasColumnName("id");

        builder.Property(pi => pi.PurchaseId)
            .HasColumnName("purchase_id")
            .IsRequired();

        builder.Property(pi => pi.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(pi => pi.ItemReference)
            .HasColumnName("item_reference")
            .HasMaxLength(100);

        builder.Property(pi => pi.Quantity)
            .HasColumnName("quantity")
            .HasDefaultValue(1);

        builder.Property(pi => pi.UnitPrice)
            .HasColumnName("unit_price")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(pi => pi.DiscountPercent)
            .HasColumnName("discount_percent")
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(0);

        builder.Property(pi => pi.TaxRate)
            .HasColumnName("tax_rate")
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(0);

        builder.Property(pi => pi.AssetId)
            .HasColumnName("asset_id");

        builder.Property(pi => pi.Comment)
            .HasColumnName("comment")
            .HasMaxLength(500);

        // Relationships
        builder.HasOne(pi => pi.Purchase)
            .WithMany(p => p.Items)
            .HasForeignKey(pi => pi.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(pi => pi.PurchaseId)
            .HasDatabaseName("ix_purchase_items_purchase_id");

        builder.HasIndex(pi => pi.AssetId)
            .HasDatabaseName("ix_purchase_items_asset_id");
    }
}
