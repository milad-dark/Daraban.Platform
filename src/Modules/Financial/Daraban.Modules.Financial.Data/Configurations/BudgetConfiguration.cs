using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Financial.Data.Configurations;

public class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("budgets");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("id");

        builder.Property(b => b.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(b => b.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(b => b.Reference)
            .HasColumnName("reference")
            .HasMaxLength(100);

        builder.Property(b => b.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(b => b.Spent)
            .HasColumnName("spent")
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0);

        builder.Property(b => b.StartDate)
            .HasColumnName("start_date")
            .IsRequired();

        builder.Property(b => b.EndDate)
            .HasColumnName("end_date")
            .IsRequired();

        builder.Property(b => b.LocationId)
            .HasColumnName("location_id");

        builder.Property(b => b.Comment)
            .HasColumnName("comment")
            .HasMaxLength(1000);

        builder.Property(b => b.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(b => b.ParentBudgetId)
            .HasColumnName("parent_budget_id");

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(b => b.CreatedById)
            .HasColumnName("created_by_id");

        builder.Property(b => b.UpdatedById)
            .HasColumnName("updated_by_id");

        builder.Property(b => b.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(b => b.DeletedAt)
            .HasColumnName("deleted_at");

        // Relationships
        builder.HasOne(b => b.ParentBudget)
            .WithMany(b => b.ChildBudgets)
            .HasForeignKey(b => b.ParentBudgetId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(b => b.EntityId)
            .HasDatabaseName("ix_budgets_entity_id");

        builder.HasIndex(b => b.Name)
            .HasDatabaseName("ix_budgets_name");

        builder.HasIndex(b => b.StartDate)
            .HasDatabaseName("ix_budgets_start_date");

        builder.HasIndex(b => b.EndDate)
            .HasDatabaseName("ix_budgets_end_date");

        builder.HasIndex(b => new { b.EntityId, b.Name })
            .IsUnique()
            .HasDatabaseName("uq_budgets_entity_name");
    }
}
