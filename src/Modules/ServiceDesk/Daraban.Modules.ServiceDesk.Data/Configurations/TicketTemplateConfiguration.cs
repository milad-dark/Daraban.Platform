using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Platform.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.ServiceDesk.Data.Configurations;

public class TicketTemplateConfiguration : IEntityTypeConfiguration<TicketTemplate>
{
    public void Configure(EntityTypeBuilder<TicketTemplate> builder)
    {
        builder.ToTable("ticket_templates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        // BaseEntity properties
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.CreatedById).HasColumnName("created_by_id");
        builder.Property(t => t.UpdatedById).HasColumnName("updated_by_id");

        // SoftDeletableEntity properties
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted").IsRequired().HasDefaultValue(false);
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");

        // TenantScopedEntity properties
        builder.Property(t => t.EntityId).HasColumnName("entity_id").IsRequired();

        // TicketTemplate properties
        builder.Property(t => t.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(t => t.DefaultType).HasColumnName("default_type").IsRequired().HasConversion<int>();
        builder.Property(t => t.DefaultPriority).HasColumnName("default_priority").IsRequired().HasConversion<int>();
        builder.Property(t => t.DefaultImpact).HasColumnName("default_impact").IsRequired().HasConversion<int>();
        builder.Property(t => t.DefaultUrgency).HasColumnName("default_urgency").IsRequired().HasConversion<int>();
        builder.Property(t => t.TitleTemplate).HasColumnName("title_template").HasMaxLength(500);
        builder.Property(t => t.DescriptionTemplate).HasColumnName("description_template").HasMaxLength(10000);
        builder.Property(t => t.DefaultCategoryId).HasColumnName("default_category_id");
        builder.Property(t => t.DefaultAssignedUserId).HasColumnName("default_assigned_user_id");
        builder.Property(t => t.DefaultAssignedGroupId).HasColumnName("default_assigned_group_id");
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(true);
        builder.Property(t => t.SortOrder).HasColumnName("sort_order").IsRequired().HasDefaultValue(0);
        builder.Property(t => t.CustomFields).HasColumnName("custom_fields").HasMaxLength(10000);

        // Indexes
        builder.HasIndex(t => t.EntityId).HasDatabaseName("ix_ticket_templates_entity_id");
        builder.HasIndex(t => t.Name).HasDatabaseName("ix_ticket_templates_name");
        builder.HasIndex(t => t.IsActive).HasDatabaseName("ix_ticket_templates_is_active");

        // Filter for soft delete
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
