using Daraban.Modules.ServiceDesk.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.ServiceDesk.Data.Configurations;

public class TicketValidationConfiguration : IEntityTypeConfiguration<TicketValidation>
{
    public void Configure(EntityTypeBuilder<TicketValidation> builder)
    {
        builder.ToTable("ticket_validations");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        // BaseEntity properties
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.CreatedById).HasColumnName("created_by_id");
        builder.Property(t => t.UpdatedById).HasColumnName("updated_by_id");

        // TicketValidation properties
        builder.Property(t => t.TicketId).HasColumnName("ticket_id").IsRequired();
        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(t => t.Status).HasColumnName("status").IsRequired().HasConversion<int>();
        builder.Property(t => t.Comment).HasColumnName("comment").HasMaxLength(2000);
        builder.Property(t => t.ValidatedAt).HasColumnName("validated_at");
        builder.Property(t => t.StepNumber).HasColumnName("step_number").IsRequired().HasDefaultValue(1);
        builder.Property(t => t.IsMandatory).HasColumnName("is_mandatory").IsRequired().HasDefaultValue(true);

        // Indexes
        builder.HasIndex(t => t.TicketId).HasDatabaseName("ix_ticket_validations_ticket_id");
        builder.HasIndex(t => t.UserId).HasDatabaseName("ix_ticket_validations_user_id");
        builder.HasIndex(t => t.Status).HasDatabaseName("ix_ticket_validations_status");

        // Relationships
        builder.HasOne(t => t.Ticket)
            .WithMany(ticket => ticket.Validations)
            .HasForeignKey(t => t.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
