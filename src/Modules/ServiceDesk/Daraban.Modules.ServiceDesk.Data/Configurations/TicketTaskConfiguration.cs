using Daraban.Modules.ServiceDesk.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.ServiceDesk.Data.Configurations;

public class TicketTaskConfiguration : IEntityTypeConfiguration<TicketTask>
{
    public void Configure(EntityTypeBuilder<TicketTask> builder)
    {
        builder.ToTable("ticket_tasks");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        // BaseEntity properties
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.CreatedById).HasColumnName("created_by_id");
        builder.Property(t => t.UpdatedById).HasColumnName("updated_by_id");

        // TicketTask properties
        builder.Property(t => t.TicketId).HasColumnName("ticket_id").IsRequired();
        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(t => t.Content).HasColumnName("content").IsRequired().HasMaxLength(10000);
        builder.Property(t => t.Type).HasColumnName("type").IsRequired().HasConversion<int>();
        builder.Property(t => t.PreviousStatus).HasColumnName("previous_status").HasConversion<int>();
        builder.Property(t => t.NewStatus).HasColumnName("new_status").HasConversion<int>();
        builder.Property(t => t.TimeSpentMinutes).HasColumnName("time_spent_minutes");
        builder.Property(t => t.IsPrivate).HasColumnName("is_private").IsRequired().HasDefaultValue(false);
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();

        // Indexes
        builder.HasIndex(t => t.TicketId).HasDatabaseName("ix_ticket_tasks_ticket_id");
        builder.HasIndex(t => t.UserId).HasDatabaseName("ix_ticket_tasks_user_id");
        builder.HasIndex(t => t.CreatedAt).HasDatabaseName("ix_ticket_tasks_created_at");

        // Relationships
        builder.HasOne(t => t.Ticket)
            .WithMany(ticket => ticket.Tasks)
            .HasForeignKey(t => t.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
