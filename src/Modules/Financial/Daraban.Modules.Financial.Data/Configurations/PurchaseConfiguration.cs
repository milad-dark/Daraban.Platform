using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Financial.Data.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("purchases");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id");

        builder.Property(p => p.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(p => p.OrderNumber)
            .HasColumnName("order_number")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(p => p.SupplierId)
            .HasColumnName("supplier_id");

        builder.Property(p => p.BudgetId)
            .HasColumnName("budget_id");

        builder.Property(p => p.RequestedDate)
            .HasColumnName("requested_date")
            .IsRequired();

        builder.Property(p => p.ApprovedDate)
            .HasColumnName("approved_date");

        builder.Property(p => p.RequestedById)
            .HasColumnName("requested_by_id")
            .IsRequired();

        builder.Property(p => p.ApprovedById)
            .HasColumnName("approved_by_id");

        builder.Property(p => p.OrderedDate)
            .HasColumnName("ordered_date");

        builder.Property(p => p.ExpectedDeliveryDate)
            .HasColumnName("expected_delivery_date");

        builder.Property(p => p.ReceivedDate)
            .HasColumnName("received_date");

        builder.Property(p => p.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(p => p.TaxAmount)
            .HasColumnName("tax_amount")
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0);

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasDefaultValue("USD");

        builder.Property(p => p.ExchangeRate)
            .HasColumnName("exchange_rate")
            .HasColumnType("decimal(10,4)");

        builder.Property(p => p.PaymentTerms)
            .HasColumnName("payment_terms")
            .HasMaxLength(100);

        builder.Property(p => p.PaymentMethod)
            .HasColumnName("payment_method")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(p => p.PaymentDate)
            .HasColumnName("payment_date");

        builder.Property(p => p.IsPaid)
            .HasColumnName("is_paid")
            .HasDefaultValue(false);

        builder.Property(p => p.DeliveryAddress)
            .HasColumnName("delivery_address")
            .HasMaxLength(500);

        builder.Property(p => p.Comment)
            .HasColumnName("comment")
            .HasMaxLength(1000);

        builder.Property(p => p.SupplierQuoteReference)
            .HasColumnName("supplier_quote_reference")
            .HasMaxLength(100);

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(p => p.CreatedById)
            .HasColumnName("created_by_id");

        builder.Property(p => p.UpdatedById)
            .HasColumnName("updated_by_id");

        builder.Property(p => p.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at");

        // Relationships
        builder.HasOne(p => p.Supplier)
            .WithMany(s => s.Purchases)
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Budget)
            .WithMany(b => b.Purchases)
            .HasForeignKey(p => p.BudgetId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(p => p.EntityId)
            .HasDatabaseName("ix_purchases_entity_id");

        builder.HasIndex(p => p.OrderNumber)
            .IsUnique()
            .HasDatabaseName("uq_purchases_order_number");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("ix_purchases_status");

        builder.HasIndex(p => p.SupplierId)
            .HasDatabaseName("ix_purchases_supplier_id");

        builder.HasIndex(p => p.RequestedDate)
            .HasDatabaseName("ix_purchases_requested_date");
    }
}
