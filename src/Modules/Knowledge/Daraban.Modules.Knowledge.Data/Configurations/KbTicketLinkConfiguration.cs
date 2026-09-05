using Daraban.Modules.Knowledge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Knowledge.Data.Configurations;

public class KbTicketLinkConfiguration : IEntityTypeConfiguration<KbTicketLink>
{
    public void Configure(EntityTypeBuilder<KbTicketLink> builder)
    {
        builder.ToTable("kb_ticket_links");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(l => l.ArticleId)
            .HasColumnName("article_id")
            .IsRequired();

        // No FK: servicedesk.tickets is another module's schema (Task 1.1 SS3).
        builder.Property(l => l.TicketId)
            .HasColumnName("ticket_id")
            .IsRequired();

        builder.Property(l => l.IsSolution)
            .HasColumnName("is_solution")
            .HasDefaultValue(false);

        builder.Property(l => l.LinkedByUserId)
            .HasColumnName("linked_by_user_id")
            .IsRequired();

        builder.Property(l => l.LinkedAt)
            .HasColumnName("linked_at")
            .IsRequired();

        builder.Property(l => l.Note)
            .HasColumnName("note")
            .HasMaxLength(1000);

        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(l => l.CreatedById).HasColumnName("created_by_id");
        builder.Property(l => l.UpdatedById).HasColumnName("updated_by_id");

        builder.HasQueryFilter(l => !l.Article.IsDeleted);

        builder.HasOne(l => l.Article)
            .WithMany(a => a.TicketLinks)
            .HasForeignKey(l => l.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.ArticleId)
            .HasDatabaseName("ix_kb_ticket_links_article_id");

        // The same article may only be linked to a given ticket once. Lookups by ticket_id
        // alone ride this index's leading column, so no separate ticket_id index is needed.
        builder.HasIndex(l => new { l.TicketId, l.ArticleId })
            .IsUnique()
            .HasDatabaseName("uq_kb_ticket_links_ticket_article");

        // A ticket has at most ONE accepted solution. Filtered unique index -- Postgres
        // enforces this; the service layer demotes any prior solution before inserting.
        builder.HasIndex(l => l.TicketId)
            .IsUnique()
            .HasFilter("is_solution = true")
            .HasDatabaseName("uq_kb_ticket_links_ticket_solution");
    }
}
