using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Platform.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.ServiceDesk.Data.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("tickets");

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

        // Ticket properties
        builder.Property(t => t.Type).HasColumnName("type").IsRequired().HasConversion<int>();
        builder.Property(t => t.Status).HasColumnName("status").IsRequired().HasConversion<int>();
        builder.Property(t => t.Priority).HasColumnName("priority").IsRequired().HasConversion<int>();
        builder.Property(t => t.Impact).HasColumnName("impact").IsRequired().HasConversion<int>();
        builder.Property(t => t.Urgency).HasColumnName("urgency").IsRequired().HasConversion<int>();
        builder.Property(t => t.CalculatedScore).HasColumnName("calculated_score");
        builder.Property(t => t.Title).HasColumnName("title").IsRequired().HasMaxLength(500);
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(10000);
        builder.Property(t => t.OpenedAt).HasColumnName("opened_at").IsRequired();
        builder.Property(t => t.LastUpdated).HasColumnName("last_updated");
        builder.Property(t => t.ClosedAt).HasColumnName("closed_at");
        builder.Property(t => t.SolvedAt).HasColumnName("solved_at");
        builder.Property(t => t.DueDate).HasColumnName("due_date");
        builder.Property(t => t.EscalationLevel).HasColumnName("escalation_level").IsRequired().HasDefaultValue(0);
        builder.Property(t => t.IsEscalated).HasColumnName("is_escalated").IsRequired().HasDefaultValue(false);
        builder.Property(t => t.RequesterUserId).HasColumnName("requester_user_id").IsRequired();
        builder.Property(t => t.AssignedUserId).HasColumnName("assigned_user_id");
        builder.Property(t => t.AssignedGroupId).HasColumnName("assigned_group_id");
        builder.Property(t => t.ItilCategoryId).HasColumnName("itil_category_id");
        builder.Property(t => t.SlaLevelId).HasColumnName("sla_level_id");
        builder.Property(t => t.AssetId).HasColumnName("asset_id");
        builder.Property(t => t.LocationId).HasColumnName("location_id");
        builder.Property(t => t.Source).HasColumnName("source").IsRequired().HasConversion<int>();
        builder.Property(t => t.ValidationStatus).HasColumnName("validation_status").IsRequired().HasConversion<int>();
        builder.Property(t => t.SatisfactionRating).HasColumnName("satisfaction_rating");
        builder.Property(t => t.SatisfactionComment).HasColumnName("satisfaction_comment").HasMaxLength(2000);

        // Indexes
        builder.HasIndex(t => t.EntityId).HasDatabaseName("ix_tickets_entity_id");
        builder.HasIndex(t => t.Status).HasDatabaseName("ix_tickets_status");
        builder.HasIndex(t => t.Priority).HasDatabaseName("ix_tickets_priority");
        builder.HasIndex(t => t.RequesterUserId).HasDatabaseName("ix_tickets_requester_user_id");
        builder.HasIndex(t => t.AssignedUserId).HasDatabaseName("ix_tickets_assigned_user_id");
        builder.HasIndex(t => t.AssignedGroupId).HasDatabaseName("ix_tickets_assigned_group_id");
        builder.HasIndex(t => t.ItilCategoryId).HasDatabaseName("ix_tickets_itil_category_id");
        builder.HasIndex(t => t.OpenedAt).HasDatabaseName("ix_tickets_opened_at");
        builder.HasIndex(t => t.DueDate).HasDatabaseName("ix_tickets_due_date");
        builder.HasIndex(t => t.Type).HasDatabaseName("ix_tickets_type");
        builder.HasIndex(t => t.IsEscalated).HasDatabaseName("ix_tickets_is_escalated");

        // Filter for soft delete
        builder.HasQueryFilter(t => !t.IsDeleted);

        // Relationships
        builder.HasMany(t => t.Tasks)
            .WithOne(task => task.Ticket)
            .HasForeignKey(task => task.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Costs)
            .WithOne(cost => cost.Ticket)
            .HasForeignKey(cost => cost.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.History)
            .WithOne(history => history.Ticket)
            .HasForeignKey(history => history.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Validations)
            .WithOne(validation => validation.Ticket)
            .HasForeignKey(validation => validation.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
