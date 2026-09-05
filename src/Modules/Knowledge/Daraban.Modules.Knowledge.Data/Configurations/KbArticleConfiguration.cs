using Daraban.Modules.Knowledge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Knowledge.Data.Configurations;

public class KbArticleConfiguration : IEntityTypeConfiguration<KbArticle>
{
    public void Configure(EntityTypeBuilder<KbArticle> builder)
    {
        builder.ToTable("kb_articles");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedNever(); // UUIDv7 generated in the service layer

        builder.Property(a => a.EntityId)
            .HasColumnName("entity_id")
            .IsRequired();

        builder.Property(a => a.Title)
            .HasColumnName("title")
            .HasMaxLength(500)
            .IsRequired();

        // No max length: Markdown bodies are unbounded in practice. Length is capped in the
        // request validator (100k chars) rather than by the column type.
        builder.Property(a => a.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(a => a.Summary)
            .HasColumnName("summary")
            .HasMaxLength(1000);

        builder.Property(a => a.CategoryId)
            .HasColumnName("category_id");

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.IsFaq)
            .HasColumnName("is_faq")
            .HasDefaultValue(false);

        builder.Property(a => a.AuthorUserId)
            .HasColumnName("author_user_id")
            .IsRequired();

        builder.Property(a => a.PublishedAt).HasColumnName("published_at");
        builder.Property(a => a.PublishedByUserId).HasColumnName("published_by_user_id");

        builder.Property(a => a.ViewCount).HasColumnName("view_count").HasDefaultValue(0);
        builder.Property(a => a.HelpfulCount).HasColumnName("helpful_count").HasDefaultValue(0);
        builder.Property(a => a.NotHelpfulCount).HasColumnName("not_helpful_count").HasDefaultValue(0);

        builder.Property(a => a.Tags)
            .HasColumnName("tags")
            .HasMaxLength(500);

        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(a => a.CreatedById).HasColumnName("created_by_id");
        builder.Property(a => a.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");

        // ---- Full-text search (Task 6.4: PostgreSQL tsvector, no Elasticsearch) ----------
        // GENERATED ALWAYS AS (to_tsvector('english', title || ' ' || content)) STORED, with a
        // GIN index over it. The 'english' config is fixed at the column level -- it must be an
        // IMMUTABLE expression, so it cannot be a runtime parameter. Postgres maintains the
        // column on every insert/update; nothing in C# ever writes to it, which is why the
        // property is marked store-generated below.
        builder.HasGeneratedTsVectorColumn(
                a => a.SearchVector,
                "english",
                a => new { a.Title, a.Content })
            .HasIndex(a => a.SearchVector)
            .HasMethod("GIN")
            .HasDatabaseName("ix_kb_articles_search_vector");

        builder.Property(a => a.SearchVector)
            .HasColumnName("search_vector");

        builder.HasQueryFilter(a => !a.IsDeleted);

        // SetNull, not Cascade: deleting a category must orphan its articles, not destroy them.
        builder.HasOne(a => a.Category)
            .WithMany(c => c.Articles)
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.EntityId)
            .HasDatabaseName("ix_kb_articles_entity_id");

        builder.HasIndex(a => a.CategoryId)
            .HasDatabaseName("ix_kb_articles_category_id");

        // Composite covering the portal's hottest query: published articles for one entity.
        builder.HasIndex(a => new { a.EntityId, a.Status })
            .HasDatabaseName("ix_kb_articles_entity_status");

        builder.HasIndex(a => a.IsFaq)
            .HasDatabaseName("ix_kb_articles_is_faq");

        builder.HasIndex(a => a.AuthorUserId)
            .HasDatabaseName("ix_kb_articles_author_user_id");
    }
}
