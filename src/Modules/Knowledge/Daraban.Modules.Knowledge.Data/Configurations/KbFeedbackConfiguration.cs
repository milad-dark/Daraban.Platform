using Daraban.Modules.Knowledge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Knowledge.Data.Configurations;

public class KbFeedbackConfiguration : IEntityTypeConfiguration<KbFeedback>
{
    public void Configure(EntityTypeBuilder<KbFeedback> builder)
    {
        builder.ToTable("kb_feedback");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(f => f.ArticleId)
            .HasColumnName("article_id")
            .IsRequired();

        builder.Property(f => f.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(f => f.IsHelpful)
            .HasColumnName("is_helpful")
            .IsRequired();

        builder.Property(f => f.Comment)
            .HasColumnName("comment")
            .HasMaxLength(2000);

        builder.Property(f => f.SubmittedAt)
            .HasColumnName("submitted_at")
            .IsRequired();

        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(f => f.CreatedById).HasColumnName("created_by_id");
        builder.Property(f => f.UpdatedById).HasColumnName("updated_by_id");

        builder.HasQueryFilter(f => !f.Article.IsDeleted);

        builder.HasOne(f => f.Article)
            .WithMany(a => a.Feedback)
            .HasForeignKey(f => f.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.ArticleId)
            .HasDatabaseName("ix_kb_feedback_article_id");

        // One verdict per user per article -- a re-submission updates the existing row.
        builder.HasIndex(f => new { f.ArticleId, f.UserId })
            .IsUnique()
            .HasDatabaseName("uq_kb_feedback_article_user");
    }
}
