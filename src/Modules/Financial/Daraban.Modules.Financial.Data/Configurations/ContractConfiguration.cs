using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Financial.Data.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("contracts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id");

        builder.Property(c => c.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.Reference)
            .HasColumnName("reference")
            .HasMaxLength(100);

        builder.Property(c => c.ContractTypeId)
            .HasColumnName("contract_type_id");

        builder.Property(c => c.SupplierId)
            .HasColumnName("supplier_id");

        builder.Property(c => c.StartDate)
            .HasColumnName("start_date")
            .IsRequired();

        builder.Property(c => c.EndDate)
            .HasColumnName("end_date");

        builder.Property(c => c.DurationMonths)
            .HasColumnName("duration_months");

        builder.Property(c => c.Value)
            .HasColumnName("value")
            .HasColumnType("decimal(18,2)");

        builder.Property(c => c.MonthlyCost)
            .HasColumnName("monthly_cost")
            .HasColumnType("decimal(18,2)");

        builder.Property(c => c.AnnualCost)
            .HasColumnName("annual_cost")
            .HasColumnType("decimal(18,2)");

        builder.Property(c => c.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasDefaultValue("USD");

        builder.Property(c => c.BillingFrequency)
            .HasColumnName("billing_frequency")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(c => c.AutoRenew)
            .HasColumnName("auto_renew")
            .HasDefaultValue(false);

        builder.Property(c => c.NoticePeriodDays)
            .HasColumnName("notice_period_days");

        builder.Property(c => c.SignedDate)
            .HasColumnName("signed_date");

        builder.Property(c => c.SignedById)
            .HasColumnName("signed_by_id");

        builder.Property(c => c.DocumentLocation)
            .HasColumnName("document_location")
            .HasMaxLength(500);

        builder.Property(c => c.Terms)
            .HasColumnName("terms")
            .HasMaxLength(2000);

        builder.Property(c => c.Comment)
            .HasColumnName("comment")
            .HasMaxLength(1000);

        builder.Property(c => c.IsCritical)
            .HasColumnName("is_critical")
            .HasDefaultValue(false);

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(c => c.CreatedById)
            .HasColumnName("created_by_id");

        builder.Property(c => c.UpdatedById)
            .HasColumnName("updated_by_id");

        builder.Property(c => c.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at");

        // Relationships
        builder.HasOne(c => c.ContractType)
            .WithMany(ct => ct.Contracts)
            .HasForeignKey(c => c.ContractTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.Supplier)
            .WithMany(s => s.Contracts)
            .HasForeignKey(c => c.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(c => c.EntityId)
            .HasDatabaseName("ix_contracts_entity_id");

        builder.HasIndex(c => c.Name)
            .HasDatabaseName("ix_contracts_name");

        builder.HasIndex(c => c.StartDate)
            .HasDatabaseName("ix_contracts_start_date");

        builder.HasIndex(c => c.EndDate)
            .HasDatabaseName("ix_contracts_end_date");

        builder.HasIndex(c => c.Status)
            .HasDatabaseName("ix_contracts_status");

        builder.HasIndex(c => c.SupplierId)
            .HasDatabaseName("ix_contracts_supplier_id");
    }
}
