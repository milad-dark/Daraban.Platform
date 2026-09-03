using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Financial.Data.Configurations;

public class InfocomConfiguration : IEntityTypeConfiguration<Infocom>
{
    public void Configure(EntityTypeBuilder<Infocom> builder)
    {
        builder.ToTable("infocoms");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id");

        builder.Property(i => i.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(i => i.AssetId)
            .HasColumnName("asset_id")
            .IsRequired();

        builder.Property(i => i.PurchaseOrderNumber)
            .HasColumnName("purchase_order_number")
            .HasMaxLength(100);

        builder.Property(i => i.InvoiceNumber)
            .HasColumnName("invoice_number")
            .HasMaxLength(100);

        builder.Property(i => i.PurchaseDate)
            .HasColumnName("purchase_date");

        builder.Property(i => i.DeliveryDate)
            .HasColumnName("delivery_date");

        builder.Property(i => i.UseDate)
            .HasColumnName("use_date");

        builder.Property(i => i.PurchaseCost)
            .HasColumnName("purchase_cost")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(i => i.AdditionalCost)
            .HasColumnName("additional_cost")
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0);

        builder.Property(i => i.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasDefaultValue("USD");

        builder.Property(i => i.SupplierId)
            .HasColumnName("supplier_id");

        builder.Property(i => i.BudgetId)
            .HasColumnName("budget_id");

        builder.Property(i => i.DepreciationMethod)
            .HasColumnName("depreciation_method")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(i => i.DepreciationDurationMonths)
            .HasColumnName("depreciation_duration_months")
            .HasDefaultValue(36);

        builder.Property(i => i.DepreciationCoefficient)
            .HasColumnName("depreciation_coefficient")
            .HasColumnType("decimal(5,2)");

        builder.Property(i => i.DepreciationOnUseDate)
            .HasColumnName("depreciation_on_use_date")
            .HasDefaultValue(true);

        builder.Property(i => i.CurrentValue)
            .HasColumnName("current_value")
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.ResidualValue)
            .HasColumnName("residual_value")
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0);

        builder.Property(i => i.WarrantyStartDate)
            .HasColumnName("warranty_start_date");

        builder.Property(i => i.WarrantyEndDate)
            .HasColumnName("warranty_end_date");

        builder.Property(i => i.WarrantyDetails)
            .HasColumnName("warranty_details")
            .HasMaxLength(1000);

        builder.Property(i => i.InsuranceStartDate)
            .HasColumnName("insurance_start_date");

        builder.Property(i => i.InsuranceEndDate)
            .HasColumnName("insurance_end_date");

        builder.Property(i => i.InsuranceValue)
            .HasColumnName("insurance_value")
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.Comment)
            .HasColumnName("comment")
            .HasMaxLength(1000);

        builder.Property(i => i.DecommissionDate)
            .HasColumnName("decommission_date");

        builder.Property(i => i.SalePrice)
            .HasColumnName("sale_price")
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(i => i.CreatedById)
            .HasColumnName("created_by_id");

        builder.Property(i => i.UpdatedById)
            .HasColumnName("updated_by_id");

        builder.Property(i => i.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(i => i.DeletedAt)
            .HasColumnName("deleted_at");

        // Relationships
        builder.HasOne(i => i.Supplier)
            .WithMany(s => s.InfocomEntries)
            .HasForeignKey(i => i.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.Budget)
            .WithMany(b => b.InfocomEntries)
            .HasForeignKey(i => i.BudgetId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(i => i.EntityId)
            .HasDatabaseName("ix_infocoms_entity_id");

        builder.HasIndex(i => i.AssetId)
            .HasDatabaseName("ix_infocoms_asset_id");

        builder.HasIndex(i => i.SupplierId)
            .HasDatabaseName("ix_infocoms_supplier_id");

        builder.HasIndex(i => i.BudgetId)
            .HasDatabaseName("ix_infocoms_budget_id");

        builder.HasIndex(i => new { i.EntityId, i.AssetId })
            .IsUnique()
            .HasDatabaseName("uq_infocoms_entity_asset");
    }
}
