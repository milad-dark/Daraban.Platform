using Daraban.Modules.Knowledge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Knowledge.Data.Configurations;

public class KbArticleTargetConfiguration : IEntityTypeConfiguration<KbArticleTarget>
{
    public void Configure(EntityTypeBuilder<KbArticleTarget> builder)
    {
        builder.ToTable("kb_article_targets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.ArticleId)
            .HasColumnName("article_id")
            .IsRequired();

        builder.Property(t => t.TargetType)
            .HasColumnName("target_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // No FK: TargetId points at identity.groups / identity.entities / identity.users
        // depending on TargetType, and Knowledge must not reference identity.* (Task 1.1 SS3).
        builder.Property(t => t.TargetId)
            .HasColumnName("target_id");

        builder.Property(t => t.IsRecursive)
            .HasColumnName("is_recursive")
            .HasDefaultValue(false);

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.CreatedById).HasColumnName("created_by_id");
        builder.Property(t => t.UpdatedById).HasColumnName("updated_by_id");

        // Mirrors KbArticle's soft-delete filter so target rows of a deleted article stay hidden
        // and EF doesn't warn about a required navigation crossing a filtered principal.
        builder.HasQueryFilter(t => !t.Article.IsDeleted);

        builder.HasOne(t => t.Article)
            .WithMany(a => a.Targets)
            .HasForeignKey(t => t.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.ArticleId)
            .HasDatabaseName("ix_kb_article_targets_article_id");

        builder.HasIndex(t => new { t.TargetType, t.TargetId })
            .HasDatabaseName("ix_kb_article_targets_target");

        builder.HasIndex(t => new { t.ArticleId, t.TargetType, t.TargetId })
            .IsUnique()
            .HasDatabaseName("uq_kb_article_targets_article_target");
    }
}
