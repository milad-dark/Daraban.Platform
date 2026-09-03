using Daraban.Modules.Software.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Software.Data.Configurations;

public class SoftwareLicenseConfiguration : IEntityTypeConfiguration<SoftwareLicense>
{
    public void Configure(EntityTypeBuilder<SoftwareLicense> builder)
    {
        builder.ToTable("software_licenses");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id");

        builder.Property(l => l.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(l => l.SoftwareId)
            .HasColumnName("software_id")
            .IsRequired();

        builder.Property(l => l.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(l => l.LicenseKey)
            .HasColumnName("license_key")
            .HasMaxLength(500);

        builder.Property(l => l.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(l => l.Quantity)
            .HasColumnName("quantity")
            .HasDefaultValue(1);

        builder.Property(l => l.UsedQuantity)
            .HasColumnName("used_quantity")
            .HasDefaultValue(0);

        builder.Property(l => l.PurchaseDate)
            .HasColumnName("purchase_date");

        builder.Property(l => l.ExpirationDate)
            .HasColumnName("expiration_date");

        builder.Property(l => l.AutoRenew)
            .HasColumnName("auto_renew")
            .HasDefaultValue(false);

        builder.Property(l => l.PurchaseCost)
            .HasColumnName("purchase_cost")
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasDefaultValue("USD");

        builder.Property(l => l.SupplierId)
            .HasColumnName("supplier_id");

        builder.Property(l => l.ContractId)
            .HasColumnName("contract_id");

        builder.Property(l => l.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(l => l.Comment)
            .HasColumnName("comment")
            .HasMaxLength(1000);

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(l => l.CreatedById)
            .HasColumnName("created_by_id");

        builder.Property(l => l.UpdatedById)
            .HasColumnName("updated_by_id");

        builder.Property(l => l.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(l => l.DeletedAt)
            .HasColumnName("deleted_at");

        // Relationships
        builder.HasOne(l => l.Software)
            .WithMany(s => s.Licenses)
            .HasForeignKey(l => l.SoftwareId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(l => l.EntityId)
            .HasDatabaseName("ix_software_licenses_entity_id");

        builder.HasIndex(l => l.SoftwareId)
            .HasDatabaseName("ix_software_licenses_software_id");

        builder.HasIndex(l => l.Type)
            .HasDatabaseName("ix_software_licenses_type");

        builder.HasIndex(l => l.ExpirationDate)
            .HasDatabaseName("ix_software_licenses_expiration_date");
    }
}
