using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Financial.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(s => s.TradingName)
            .HasColumnName("trading_name")
            .HasMaxLength(255);

        builder.Property(s => s.ContactName)
            .HasColumnName("contact_name")
            .HasMaxLength(255);

        builder.Property(s => s.Email)
            .HasColumnName("email")
            .HasMaxLength(255);

        builder.Property(s => s.Phone)
            .HasColumnName("phone")
            .HasMaxLength(50);

        builder.Property(s => s.Mobile)
            .HasColumnName("mobile")
            .HasMaxLength(50);

        builder.Property(s => s.Fax)
            .HasColumnName("fax")
            .HasMaxLength(50);

        builder.Property(s => s.Website)
            .HasColumnName("website")
            .HasMaxLength(500);

        builder.Property(s => s.AddressLine1)
            .HasColumnName("address_line1")
            .HasMaxLength(255);

        builder.Property(s => s.AddressLine2)
            .HasColumnName("address_line2")
            .HasMaxLength(255);

        builder.Property(s => s.City)
            .HasColumnName("city")
            .HasMaxLength(100);

        builder.Property(s => s.State)
            .HasColumnName("state")
            .HasMaxLength(100);

        builder.Property(s => s.PostalCode)
            .HasColumnName("postal_code")
            .HasMaxLength(20);

        builder.Property(s => s.Country)
            .HasColumnName("country")
            .HasMaxLength(100);

        builder.Property(s => s.RegistrationNumber)
            .HasColumnName("registration_number")
            .HasMaxLength(100);

        builder.Property(s => s.VatNumber)
            .HasColumnName("vat_number")
            .HasMaxLength(100);

        builder.Property(s => s.Iban)
            .HasColumnName("iban")
            .HasMaxLength(100);

        builder.Property(s => s.BankName)
            .HasColumnName("bank_name")
            .HasMaxLength(255);

        builder.Property(s => s.SortCode)
            .HasColumnName("sort_code")
            .HasMaxLength(20);

        builder.Property(s => s.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(s => s.Comment)
            .HasColumnName("comment")
            .HasMaxLength(1000);

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(s => s.CreatedById)
            .HasColumnName("created_by_id");

        builder.Property(s => s.UpdatedById)
            .HasColumnName("updated_by_id");

        builder.Property(s => s.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(s => s.DeletedAt)
            .HasColumnName("deleted_at");

        // Indexes
        builder.HasIndex(s => s.EntityId)
            .HasDatabaseName("ix_suppliers_entity_id");

        builder.HasIndex(s => s.Name)
            .HasDatabaseName("ix_suppliers_name");

        builder.HasIndex(s => s.Email)
            .HasDatabaseName("ix_suppliers_email");

        builder.HasIndex(s => s.IsActive)
            .HasDatabaseName("ix_suppliers_is_active");
    }
}
