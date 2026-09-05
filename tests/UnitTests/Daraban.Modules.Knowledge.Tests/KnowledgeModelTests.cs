using Daraban.Modules.Knowledge.Data;
using Daraban.Modules.Knowledge.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Daraban.Modules.Knowledge.Tests;

/// <summary>
/// Builds the real EF Core model against the Npgsql provider and asserts the mapping
/// decisions Task 6.4 depends on. No database is contacted -- model building is offline, so
/// these run in CI without Postgres. They catch the failures that would otherwise only show
/// up at "dotnet ef migrations add" or first request: a missing schema, a tsvector column
/// that isn't actually store-generated, a lost GIN index, or a soft-delete filter that
/// silently stopped applying.
/// </summary>
public class KnowledgeModelTests
{
    private static KnowledgeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            // A syntactically valid connection string is enough -- nothing here opens it.
            .UseNpgsql("Host=localhost;Database=daraban_model_test;Username=x;Password=y")
            .Options;

        return new KnowledgeDbContext(options);
    }

    private static IEntityType EntityTypeOf<T>() where T : class
    {
        using var context = CreateContext();
        return context.Model.FindEntityType(typeof(T))!;
    }

    [Fact]
    public void Model_Builds_Without_Error()
    {
        using var context = CreateContext();

        // Touching Model forces the whole model to be built and validated.
        Assert.NotNull(context.Model);
    }

    [Theory]
    [InlineData(typeof(KbCategory), "kb_categories")]
    [InlineData(typeof(KbArticle), "kb_articles")]
    [InlineData(typeof(KbArticleTarget), "kb_article_targets")]
    [InlineData(typeof(KbFeedback), "kb_feedback")]
    [InlineData(typeof(KbTicketLink), "kb_ticket_links")]
    public void EveryEntity_Maps_To_KnowledgeSchema(Type clrType, string expectedTable)
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(clrType);

        Assert.NotNull(entityType);
        Assert.Equal(expectedTable, entityType!.GetTableName());

        // Schema-per-module (Task 1.2). A table landing in "public" would collide across modules.
        Assert.Equal("knowledge", entityType.GetSchema());
    }

    [Fact]
    public void KbArticle_SearchVector_Is_StoreGenerated_TsVector()
    {
        var article = EntityTypeOf<KbArticle>();
        var searchVector = article.FindProperty(nameof(KbArticle.SearchVector));

        Assert.NotNull(searchVector);
        Assert.Equal("search_vector", searchVector!.GetColumnName());
        Assert.Equal("tsvector", searchVector.GetColumnType());

        // The whole point of the GENERATED ALWAYS column: Postgres computes it, EF never writes
        // it. If this ever flips to None, inserts would try to send a null tsvector and fail.
        Assert.Equal(ValueGenerated.OnAddOrUpdate, searchVector.ValueGenerated);
    }

    [Fact]
    public void KbArticle_SearchVector_Has_Gin_Index()
    {
        var article = EntityTypeOf<KbArticle>();

        var ginIndex = article.GetIndexes().SingleOrDefault(i =>
            i.Properties.Count == 1 &&
            i.Properties[0].Name == nameof(KbArticle.SearchVector));

        Assert.NotNull(ginIndex);

        // Without GIN, every search degrades to a sequential scan over the whole table.
        Assert.Equal("GIN", ginIndex!.GetMethod());
        Assert.Equal("ix_kb_articles_search_vector", ginIndex.GetDatabaseName());
    }

    [Fact]
    public void KbCategory_Slug_Is_Unique_Per_Entity()
    {
        var category = EntityTypeOf<KbCategory>();

        var slugIndex = category.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(KbCategory.EntityId), nameof(KbCategory.Slug) }));

        Assert.NotNull(slugIndex);
        Assert.True(slugIndex!.IsUnique);
    }

    [Fact]
    public void KbCategory_Parent_Uses_Restrict_So_Subtrees_Are_Not_Cascaded()
    {
        var category = EntityTypeOf<KbCategory>();

        var parentFk = category.GetForeignKeys().Single(fk =>
            fk.Properties.Single().Name == nameof(KbCategory.ParentId));

        Assert.Equal(DeleteBehavior.Restrict, parentFk.DeleteBehavior);
    }

    [Fact]
    public void KbArticle_Category_Uses_SetNull_So_Articles_Survive_Category_Deletion()
    {
        var article = EntityTypeOf<KbArticle>();

        var categoryFk = article.GetForeignKeys().Single(fk =>
            fk.Properties.Single().Name == nameof(KbArticle.CategoryId));

        Assert.Equal(DeleteBehavior.SetNull, categoryFk.DeleteBehavior);
    }

    [Fact]
    public void KbFeedback_Is_Unique_Per_Article_And_User()
    {
        var feedback = EntityTypeOf<KbFeedback>();

        var index = feedback.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(KbFeedback.ArticleId), nameof(KbFeedback.UserId) }));

        Assert.NotNull(index);

        // One verdict per reader -- this is what makes SubmitFeedbackAsync an upsert rather
        // than an append.
        Assert.True(index!.IsUnique);
    }

    [Fact]
    public void KbTicketLink_Allows_Only_One_Solution_Per_Ticket()
    {
        var link = EntityTypeOf<KbTicketLink>();

        var solutionIndex = link.GetIndexes().SingleOrDefault(i =>
            i.Properties.Count == 1 &&
            i.Properties[0].Name == nameof(KbTicketLink.TicketId) &&
            i.GetDatabaseName() == "uq_kb_ticket_links_ticket_solution");

        Assert.NotNull(solutionIndex);
        Assert.True(solutionIndex!.IsUnique);

        // Partial index -- the uniqueness only binds the rows that claim to be the solution.
        Assert.Equal("is_solution = true", solutionIndex.GetFilter());
    }

    [Fact]
    public void KbTicketLink_Is_Unique_Per_Ticket_And_Article()
    {
        var link = EntityTypeOf<KbTicketLink>();

        var index = link.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(
                new[] { nameof(KbTicketLink.TicketId), nameof(KbTicketLink.ArticleId) }));

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
    }

    [Theory]
    [InlineData(typeof(KbCategory))]
    [InlineData(typeof(KbArticle))]
    [InlineData(typeof(KbArticleTarget))]
    [InlineData(typeof(KbFeedback))]
    [InlineData(typeof(KbTicketLink))]
    public void EveryEntity_Has_A_SoftDelete_QueryFilter(Type clrType)
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(clrType)!;

        // Child tables filter on the parent article's IsDeleted rather than their own column,
        // so the assertion is only that a filter exists at all -- but it must exist, or a
        // soft-deleted article's targets/feedback/links would keep showing up.
        Assert.NotEmpty(entityType.GetDeclaredQueryFilters());
    }

    [Fact]
    public void Enums_Are_Persisted_As_Strings_Not_Ordinals()
    {
        var article = EntityTypeOf<KbArticle>();
        var status = article.FindProperty(nameof(KbArticle.Status))!;

        // String storage means renumbering an enum member later can't silently reinterpret
        // existing rows.
        Assert.Equal(typeof(string), status.GetProviderClrType());

        var target = EntityTypeOf<KbArticleTarget>();
        var targetType = target.FindProperty(nameof(KbArticleTarget.TargetType))!;
        Assert.Equal(typeof(string), targetType.GetProviderClrType());
    }

    [Fact]
    public void Keys_Are_Never_Database_Generated_Because_Ids_Are_Uuidv7_From_Code()
    {
        using var context = CreateContext();

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            var key = entityType.FindPrimaryKey()!.Properties.Single();
            Assert.Equal(ValueGenerated.Never, key.ValueGenerated);
        }
    }
}
