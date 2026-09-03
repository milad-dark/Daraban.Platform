using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Financial.Data.Configurations;

public class ContractCostConfiguration : IEntityTypeConfiguration<ContractCost>
{
    public void Configure(EntityTypeBuilder<ContractCost> builder)
    {
        builder.ToTable("contract_costs");

        builder.HasKey(cc => cc.Id);

        builder.Property(cc => cc.Id)
            .HasColumnName("id");

        builder.Property(cc => cc.ContractId)
            .HasColumnName("contract_id")
            .IsRequired();

        builder.Property(cc => cc.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(cc => cc.PeriodStart)
            .HasColumnName("period_start")
            .IsRequired();

        builder.Property(cc => cc.PeriodEnd)
            .HasColumnName("period_end")
            .IsRequired();

        builder.Property(cc => cc.InvoiceReference)
            .HasColumnName("invoice_reference")
            .HasMaxLength(100);

        builder.Property(cc => cc.InvoiceDate)
            .HasColumnName("invoice_date");

        builder.Property(cc => cc.IsPaid)
            .HasColumnName("is_paid")
            .HasDefaultValue(false);

        builder.Property(cc => cc.PaidDate)
            .HasColumnName("paid_date");

        builder.Property(cc => cc.Comment)
            .HasColumnName("comment")
            .HasMaxLength(1000);

        builder.Property(cc => cc.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Relationships
        builder.HasOne(cc => cc.Contract)
            .WithMany(c => c.ContractCosts)
            .HasForeignKey(cc => cc.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(cc => cc.ContractId)
            .HasDatabaseName("ix_contract_costs_contract_id");

        builder.HasIndex(cc => cc.PeriodStart)
            .HasDatabaseName("ix_contract_costs_period_start");

        builder.HasIndex(cc => cc.IsPaid)
            .HasDatabaseName("ix_contract_costs_is_paid");
    }
}
