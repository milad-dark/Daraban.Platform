using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Financial.Data.Configurations;

public class ContractTypeConfiguration : IEntityTypeConfiguration<ContractType>
{
    public void Configure(EntityTypeBuilder<ContractType> builder)
    {
        builder.ToTable("contract_types");

        builder.HasKey(ct => ct.Id);

        builder.Property(ct => ct.Id)
            .HasColumnName("id");

        builder.Property(ct => ct.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(ct => ct.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(ct => ct.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(ct => ct.DefaultDurationMonths)
            .HasColumnName("default_duration_months");

        builder.Property(ct => ct.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(ct => ct.SortOrder)
            .HasColumnName("sort_order")
            .HasDefaultValue(0);

        builder.Property(ct => ct.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(ct => ct.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(ct => ct.CreatedById)
            .HasColumnName("created_by_id");

        builder.Property(ct => ct.UpdatedById)
            .HasColumnName("updated_by_id");

        builder.Property(ct => ct.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(ct => ct.DeletedAt)
            .HasColumnName("deleted_at");

        // Indexes
        builder.HasIndex(ct => ct.EntityId)
            .HasDatabaseName("ix_contract_types_entity_id");

        builder.HasIndex(ct => new { ct.EntityId, ct.Name })
            .IsUnique()
            .HasDatabaseName("uq_contract_types_entity_name");
    }
}
