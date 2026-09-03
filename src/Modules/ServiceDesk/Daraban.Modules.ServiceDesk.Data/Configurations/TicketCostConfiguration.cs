using Daraban.Modules.ServiceDesk.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.ServiceDesk.Data.Configurations;

public class TicketCostConfiguration : IEntityTypeConfiguration<TicketCost>
{
    public void Configure(EntityTypeBuilder<TicketCost> builder)
    {
        builder.ToTable("ticket_costs");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        // BaseEntity properties
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.CreatedById).HasColumnName("created_by_id");
        builder.Property(t => t.UpdatedById).HasColumnName("updated_by_id");

        // TicketCost properties
        builder.Property(t => t.TicketId).HasColumnName("ticket_id").IsRequired();
        builder.Property(t => t.CostType).HasColumnName("cost_type").IsRequired().HasConversion<int>();
        builder.Property(t => t.Description).HasColumnName("description").IsRequired().HasMaxLength(500);
        builder.Property(t => t.Amount).HasColumnName("amount").IsRequired().HasPrecision(18, 2);
        builder.Property(t => t.Currency).HasColumnName("currency").IsRequired().HasMaxLength(3);
        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(t => t.IncurredAt).HasColumnName("incurred_at").IsRequired();
        builder.Property(t => t.Reference).HasColumnName("reference").HasMaxLength(200);

        // Indexes
        builder.HasIndex(t => t.TicketId).HasDatabaseName("ix_ticket_costs_ticket_id");
        builder.HasIndex(t => t.UserId).HasDatabaseName("ix_ticket_costs_user_id");
        builder.HasIndex(t => t.CostType).HasDatabaseName("ix_ticket_costs_cost_type");
        builder.HasIndex(t => t.IncurredAt).HasDatabaseName("ix_ticket_costs_incurred_at");

        // Relationships
        builder.HasOne(t => t.Ticket)
            .WithMany(ticket => ticket.Costs)
            .HasForeignKey(t => t.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
