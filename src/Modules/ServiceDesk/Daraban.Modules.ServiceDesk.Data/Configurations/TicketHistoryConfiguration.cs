using Daraban.Modules.ServiceDesk.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.ServiceDesk.Data.Configurations;

public class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistory>
{
    public void Configure(EntityTypeBuilder<TicketHistory> builder)
    {
        builder.ToTable("ticket_histories");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        // BaseEntity properties
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.CreatedById).HasColumnName("created_by_id");
        builder.Property(t => t.UpdatedById).HasColumnName("updated_by_id");

        // TicketHistory properties
        builder.Property(t => t.TicketId).HasColumnName("ticket_id").IsRequired();
        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(t => t.FieldName).HasColumnName("field_name").IsRequired().HasMaxLength(100);
        builder.Property(t => t.OldValue).HasColumnName("old_value").HasMaxLength(5000);
        builder.Property(t => t.NewValue).HasColumnName("new_value").HasMaxLength(5000);
        builder.Property(t => t.Action).HasColumnName("action").IsRequired().HasConversion<int>();
        builder.Property(t => t.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(t => t.Comment).HasColumnName("comment").HasMaxLength(2000);

        // Indexes
        builder.HasIndex(t => t.TicketId).HasDatabaseName("ix_ticket_histories_ticket_id");
        builder.HasIndex(t => t.UserId).HasDatabaseName("ix_ticket_histories_user_id");
        builder.HasIndex(t => t.FieldName).HasDatabaseName("ix_ticket_histories_field_name");
        builder.HasIndex(t => t.Action).HasDatabaseName("ix_ticket_histories_action");
        builder.HasIndex(t => t.OccurredAt).HasDatabaseName("ix_ticket_histories_occurred_at");

        // Relationships
        builder.HasOne(t => t.Ticket)
            .WithMany(ticket => ticket.History)
            .HasForeignKey(t => t.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
