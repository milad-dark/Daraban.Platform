using Daraban.Modules.Software.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Software.Data.Configurations;

public class SoftwareConfiguration : IEntityTypeConfiguration<Software>
{
    public void Configure(EntityTypeBuilder<Software> builder)
    {
        builder.ToTable("softwares");

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

        builder.Property(s => s.Version)
            .HasColumnName("version")
            .HasMaxLength(50);

        builder.Property(s => s.Editor)
            .HasColumnName("editor")
            .HasMaxLength(255);

        builder.Property(s => s.Description)
            .HasColumnName("description")
            .HasMaxLength(1000);

        builder.Property(s => s.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.Edition)
            .HasColumnName("edition")
            .HasMaxLength(100);

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(s => s.IsOpenSource)
            .HasColumnName("is_open_source")
            .HasDefaultValue(false);

        builder.Property(s => s.IsFree)
            .HasColumnName("is_free")
            .HasDefaultValue(false);

        builder.Property(s => s.Website)
            .HasColumnName("website")
            .HasMaxLength(500);

        builder.Property(s => s.DocumentationUrl)
            .HasColumnName("documentation_url")
            .HasMaxLength(500);

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
            .HasDatabaseName("ix_softwares_entity_id");

        builder.HasIndex(s => s.Name)
            .HasDatabaseName("ix_softwares_name");

        builder.HasIndex(s => s.Category)
            .HasDatabaseName("ix_softwares_category");

        builder.HasIndex(s => new { s.EntityId, s.Name, s.Version })
            .IsUnique()
            .HasDatabaseName("uq_softwares_entity_name_version");
    }
}
