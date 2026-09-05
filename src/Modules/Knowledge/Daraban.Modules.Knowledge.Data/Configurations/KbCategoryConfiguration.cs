using Daraban.Modules.Knowledge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Knowledge.Data.Configurations;

public class KbCategoryConfiguration : IEntityTypeConfiguration<KbCategory>
{
    public void Configure(EntityTypeBuilder<KbCategory> builder)
    {
        builder.ToTable("kb_categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedNever(); // UUIDv7 generated in the service layer, not by the DB

        builder.Property(c => c.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(c => c.ParentId)
            .HasColumnName("parent_id");

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Slug)
            .HasColumnName("slug")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(c => c.SortOrder)
            .HasColumnName("sort_order")
            .HasDefaultValue(0);

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(c => c.CreatedById).HasColumnName("created_by_id");
        builder.Property(c => c.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");

        // Soft-deleted categories disappear from every query unless IgnoreQueryFilters() is used.
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Restrict, not Cascade: deleting a parent must not silently take its subtree with it.
        // KbCategoryService rejects deletion while children exist.
        builder.HasOne(c => c.Parent)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.EntityId)
            .HasDatabaseName("ix_kb_categories_entity_id");

        builder.HasIndex(c => c.ParentId)
            .HasDatabaseName("ix_kb_categories_parent_id");

        builder.HasIndex(c => new { c.EntityId, c.Slug })
            .IsUnique()
            .HasDatabaseName("uq_kb_categories_entity_slug");
    }
}
