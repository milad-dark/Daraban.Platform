using Daraban.Modules.Knowledge.Data.Entities;
using Daraban.Modules.Knowledge.Data.Repositories;
using Daraban.Modules.Knowledge.Services;
using Daraban.Modules.Knowledge.Services.Dtos;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Contracts.Knowledge;
using Moq;
using Xunit;

namespace Daraban.Modules.Knowledge.Tests;

/// <summary>
/// Behaviour of KbArticleService with every repository mocked -- the status state machine,
/// audience-target validation, the feedback upsert, and which domain events actually get
/// published. No database, no HTTP.
/// </summary>
public class KbArticleServiceTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IKbArticleRepository> _articles = new(MockBehavior.Strict);
    private readonly Mock<IKbCategoryRepository> _categories = new(MockBehavior.Strict);
    private readonly Mock<IKbFeedbackRepository> _feedback = new(MockBehavior.Strict);
    private readonly Mock<IEventPublisher> _events = new();

    private KbArticleService CreateSut() =>
        new(_articles.Object, _categories.Object, _feedback.Object, _events.Object);

    private static KbArticle Article(
        KbArticleStatus status = KbArticleStatus.Draft,
        string content = "How to reset the VPN client.",
        Guid? id = null)
        => new()
        {
            Id = id ?? Guid.CreateVersion7(),
            EntityId = EntityId,
            Title = "VPN reset",
            Content = content,
            Status = status,
            AuthorUserId = ActorId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    // ---- Create ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Always_Starts_As_Draft()
    {
        _articles.Setup(r => r.AddAsync(It.IsAny<KbArticle>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new CreateKbArticleRequest(
            "VPN reset", "Steps...", null, null, IsFaq: false, Tags: null, Targets: null);

        var result = await CreateSut().CreateAsync(request, EntityId, ActorId);

        Assert.True(result.IsSuccess);

        // An article must never go live as a side effect of creation -- publishing is its own
        // explicit transition with its own permission.
        Assert.Equal(KbArticleStatus.Draft, result.Value.Status);
        Assert.Equal(ActorId, result.Value.AuthorUserId);
    }

    [Fact]
    public async Task CreateAsync_Rejects_Category_From_Another_Entity()
    {
        var foreignCategoryId = Guid.CreateVersion7();
        _categories.Setup(r => r.GetByIdAsync(foreignCategoryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KbCategory { Id = foreignCategoryId, EntityId = Guid.CreateVersion7() });

        var request = new CreateKbArticleRequest(
            "T", "C", null, foreignCategoryId, false, null, null);

        var result = await CreateSut().CreateAsync(request, EntityId, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.CATEGORY_CROSS_ENTITY", result.Error!.Code);
        _articles.Verify(r => r.AddAsync(It.IsAny<KbArticle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Normalizes_Ragged_Tags()
    {
        KbArticle? captured = null;
        _articles.Setup(r => r.AddAsync(It.IsAny<KbArticle>(), It.IsAny<CancellationToken>()))
            .Callback<KbArticle, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new CreateKbArticleRequest(
            "T", "C", null, null, false, "vpn, , Wifi ,", null);

        var result = await CreateSut().CreateAsync(request, EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("vpn,Wifi", captured!.Tags);
    }

    [Theory]
    [InlineData(KbTargetType.Group, null, "KNOWLEDGE.TARGET_ID_REQUIRED")]
    [InlineData(KbTargetType.Entity, null, "KNOWLEDGE.TARGET_ID_REQUIRED")]
    public async Task CreateAsync_Rejects_Non_All_Target_Without_Id(
        KbTargetType targetType, Guid? targetId, string expectedCode)
    {
        var request = new CreateKbArticleRequest(
            "T", "C", null, null, false, null,
            new[] { new KbArticleTargetInput(targetType, targetId) });

        var result = await CreateSut().CreateAsync(request, EntityId, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_Rejects_All_Target_Carrying_An_Id()
    {
        var request = new CreateKbArticleRequest(
            "T", "C", null, null, false, null,
            new[] { new KbArticleTargetInput(KbTargetType.All, Guid.CreateVersion7()) });

        var result = await CreateSut().CreateAsync(request, EntityId, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.TARGET_ID_NOT_ALLOWED", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_Rejects_Duplicate_Targets_Before_The_Database_Does()
    {
        var groupId = Guid.CreateVersion7();
        var request = new CreateKbArticleRequest(
            "T", "C", null, null, false, null,
            new[]
            {
                new KbArticleTargetInput(KbTargetType.Group, groupId),
                new KbArticleTargetInput(KbTargetType.Group, groupId),
            });

        var result = await CreateSut().CreateAsync(request, EntityId, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.TARGET_DUPLICATE", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_Forces_IsRecursive_False_For_Non_Entity_Targets()
    {
        KbArticle? captured = null;
        _articles.Setup(r => r.AddAsync(It.IsAny<KbArticle>(), It.IsAny<CancellationToken>()))
            .Callback<KbArticle, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new CreateKbArticleRequest(
            "T", "C", null, null, false, null,
            new[] { new KbArticleTargetInput(KbTargetType.Group, Guid.CreateVersion7(), IsRecursive: true) });

        await CreateSut().CreateAsync(request, EntityId, ActorId);

        // Recursion is an entity-tree concept; a recursive group target is meaningless and
        // would be misleading if persisted.
        Assert.False(captured!.Targets.Single().IsRecursive);
    }

    // ---- Status transitions ---------------------------------------------------------------

    [Theory]
    [InlineData(KbArticleStatus.Draft, KbArticleStatus.Published)]
    [InlineData(KbArticleStatus.Draft, KbArticleStatus.Archived)]
    [InlineData(KbArticleStatus.Published, KbArticleStatus.Draft)]
    [InlineData(KbArticleStatus.Published, KbArticleStatus.Archived)]
    [InlineData(KbArticleStatus.Archived, KbArticleStatus.Draft)]
    public async Task ChangeStatusAsync_Allows_Valid_Transitions(
        KbArticleStatus from, KbArticleStatus to)
    {
        var article = Article(from);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _articles.Setup(r => r.Update(article));
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().ChangeStatusAsync(article.Id, to, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(to, result.Value.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_Refuses_Archived_Straight_To_Published()
    {
        var article = Article(KbArticleStatus.Archived);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);

        var result = await CreateSut().ChangeStatusAsync(article.Id, KbArticleStatus.Published, ActorId);

        // Archived content goes back through Draft so somebody looks at it before it is live again.
        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.ARTICLE_INVALID_TRANSITION", result.Error!.Code);
        _articles.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangeStatusAsync_Refuses_A_No_Op_Transition()
    {
        var article = Article(KbArticleStatus.Published);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);

        var result = await CreateSut().ChangeStatusAsync(article.Id, KbArticleStatus.Published, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.ARTICLE_STATUS_UNCHANGED", result.Error!.Code);
    }

    [Fact]
    public async Task ChangeStatusAsync_Refuses_To_Publish_Empty_Content()
    {
        var article = Article(KbArticleStatus.Draft, content: "   ");
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);

        var result = await CreateSut().ChangeStatusAsync(article.Id, KbArticleStatus.Published, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.ARTICLE_EMPTY_CONTENT", result.Error!.Code);
    }

    [Fact]
    public async Task ChangeStatusAsync_Publishing_Stamps_Metadata_And_Emits_Event()
    {
        var article = Article(KbArticleStatus.Draft);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _articles.Setup(r => r.Update(article));
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().ChangeStatusAsync(article.Id, KbArticleStatus.Published, ActorId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.PublishedAt);
        Assert.Equal(ActorId, result.Value.PublishedByUserId);

        _events.Verify(e => e.PublishAsync(
            It.Is<KbArticlePublishedEvent>(evt => evt.ArticleId == article.Id && evt.EntityId == EntityId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangeStatusAsync_Unpublishing_Emits_Unpublished_Event()
    {
        var article = Article(KbArticleStatus.Published);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _articles.Setup(r => r.Update(article));
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().ChangeStatusAsync(article.Id, KbArticleStatus.Archived, ActorId);

        _events.Verify(e => e.PublishAsync(
            It.Is<KbArticleUnpublishedEvent>(evt => evt.NewStatus == "Archived"),
            It.IsAny<CancellationToken>()), Times.Once);
        _events.Verify(e => e.PublishAsync(
            It.IsAny<KbArticlePublishedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_And_Signals_When_It_Was_Live()
    {
        var article = Article(KbArticleStatus.Published);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _articles.Setup(r => r.Update(article));
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().DeleteAsync(article.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(article.IsDeleted);
        Assert.NotNull(article.DeletedAt);

        // A deleted article vanishes from the portal exactly like an unpublished one, so
        // subscribers need the same signal.
        _events.Verify(e => e.PublishAsync(
            It.Is<KbArticleUnpublishedEvent>(evt => evt.NewStatus == "Deleted"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Of_A_Draft_Emits_Nothing()
    {
        var article = Article(KbArticleStatus.Draft);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _articles.Setup(r => r.Update(article));
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().DeleteAsync(article.Id, ActorId);

        _events.Verify(e => e.PublishAsync(
            It.IsAny<KbArticleUnpublishedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Update ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_Refuses_To_Edit_An_Archived_Article()
    {
        var article = Article(KbArticleStatus.Archived);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);

        var request = new UpdateKbArticleRequest("New", "Body", null, null, false, null, null);
        var result = await CreateSut().UpdateAsync(article.Id, request, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.ARTICLE_ARCHIVED", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_With_Null_Targets_Leaves_Targeting_Alone()
    {
        var article = Article(KbArticleStatus.Draft);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _articles.Setup(r => r.Update(article));
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _articles.Setup(r => r.GetByIdWithDetailsAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);

        var request = new UpdateKbArticleRequest("New title", "Body", null, null, false, null, Targets: null);
        var result = await CreateSut().UpdateAsync(article.Id, request, ActorId);

        Assert.True(result.IsSuccess);

        // null Targets means "don't touch"; only a non-null list (including an empty one) rewrites.
        _articles.Verify(r => r.ReplaceTargetsAsync(
            It.IsAny<Guid>(), It.IsAny<IEnumerable<KbArticleTarget>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_With_Empty_Targets_Clears_Them()
    {
        var article = Article(KbArticleStatus.Draft);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _articles.Setup(r => r.Update(article));
        _articles.Setup(r => r.ReplaceTargetsAsync(
                article.Id, It.IsAny<IEnumerable<KbArticleTarget>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _articles.Setup(r => r.GetByIdWithDetailsAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);

        var request = new UpdateKbArticleRequest(
            "New title", "Body", null, null, false, null, Targets: Array.Empty<KbArticleTargetInput>());

        var result = await CreateSut().UpdateAsync(article.Id, request, ActorId);

        Assert.True(result.IsSuccess);
        _articles.Verify(r => r.ReplaceTargetsAsync(
            article.Id, It.IsAny<IEnumerable<KbArticleTarget>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Feedback -------------------------------------------------------------------------

    [Fact]
    public async Task SubmitFeedbackAsync_Refuses_Unpublished_Articles()
    {
        var article = Article(KbArticleStatus.Draft);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);

        var result = await CreateSut().SubmitFeedbackAsync(
            article.Id, new SubmitKbFeedbackRequest(true, null), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.ARTICLE_NOT_PUBLISHED", result.Error!.Code);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_Inserts_A_First_Verdict_And_Refreshes_Counters()
    {
        var article = Article(KbArticleStatus.Published);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _articles.Setup(r => r.Update(article));
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _feedback.Setup(r => r.GetByArticleAndUserAsync(article.Id, ActorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KbFeedback?)null);
        _feedback.Setup(r => r.AddAsync(It.IsAny<KbFeedback>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _feedback.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _feedback.Setup(r => r.CountVerdictsAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((3, 1));

        var result = await CreateSut().SubmitFeedbackAsync(
            article.Id, new SubmitKbFeedbackRequest(true, "Worked"), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.HelpfulCount);
        Assert.Equal(1, result.Value.NotHelpfulCount);

        // Counters come from a recount, not +1 arithmetic, so they cannot drift from the rows.
        Assert.Equal(3, article.HelpfulCount);
        Assert.Equal(1, article.NotHelpfulCount);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_Revises_An_Existing_Verdict_Instead_Of_Adding_A_Second()
    {
        var article = Article(KbArticleStatus.Published);
        var existing = new KbFeedback
        {
            Id = Guid.CreateVersion7(),
            ArticleId = article.Id,
            UserId = ActorId,
            IsHelpful = true,
            Comment = "Worked",
        };

        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _articles.Setup(r => r.Update(article));
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _feedback.Setup(r => r.GetByArticleAndUserAsync(article.Id, ActorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _feedback.Setup(r => r.Update(existing));
        _feedback.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _feedback.Setup(r => r.CountVerdictsAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, 1));

        var result = await CreateSut().SubmitFeedbackAsync(
            article.Id, new SubmitKbFeedbackRequest(false, "Outdated"), ActorId);

        Assert.True(result.IsSuccess);
        Assert.False(existing.IsHelpful);
        Assert.Equal("Outdated", existing.Comment);

        // One row per reader per article -- never a second vote.
        _feedback.Verify(r => r.AddAsync(It.IsAny<KbFeedback>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Search + read --------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_Rejects_A_Blank_Query(string query)
    {
        var result = await CreateSut().SearchAsync(EntityId, query, null, null, 1, 20);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.SEARCH_QUERY_REQUIRED", result.Error!.Code);
    }

    [Fact]
    public async Task SearchAsync_Trims_The_Query_And_Passes_It_Through()
    {
        _articles.Setup(r => r.SearchAsync(
                EntityId, "vpn", null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<KbSearchHit>(), 0));

        var result = await CreateSut().SearchAsync(EntityId, "  vpn  ", null, null, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal("vpn", result.Value.Query);
    }

    [Theory]
    [InlineData(0, 20, 1, 20)]      // page below 1 is clamped up
    [InlineData(-5, 20, 1, 20)]
    [InlineData(3, 0, 3, 20)]       // pageSize below 1 falls back to the default
    [InlineData(1, 5000, 1, 200)]   // pageSize is capped so one request can't pull the table
    public async Task GetPagedAsync_Normalizes_Paging(
        int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        _articles.Setup(r => r.GetPagedAsync(
                EntityId, null, null, null, null, null, expectedPage, expectedPageSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<KbArticle>(), 0));

        var result = await CreateSut().GetPagedAsync(
            EntityId, null, null, null, null, null, page, pageSize);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedPage, result.Value.Page);
        Assert.Equal(expectedPageSize, result.Value.PageSize);
    }

    [Fact]
    public async Task GetByIdAsync_Counts_A_View_Only_When_Asked()
    {
        var article = Article(KbArticleStatus.Published);
        article.ViewCount = 7;

        _articles.Setup(r => r.GetByIdWithDetailsAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);
        _articles.Setup(r => r.IncrementViewCountAsync(article.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var counted = await CreateSut().GetByIdAsync(article.Id, incrementViewCount: true);

        Assert.True(counted.IsSuccess);
        // ExecuteUpdateAsync bypasses the tracker, so the service mirrors the increment locally
        // to keep the response consistent with the stored row.
        Assert.Equal(8, counted.Value.ViewCount);
        _articles.Verify(r => r.IncrementViewCountAsync(article.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_Editor_Read_Does_Not_Inflate_ViewCount()
    {
        var article = Article(KbArticleStatus.Draft);
        _articles.Setup(r => r.GetByIdWithDetailsAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);

        var result = await CreateSut().GetByIdAsync(article.Id);

        Assert.True(result.IsSuccess);
        _articles.Verify(r => r.IncrementViewCountAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_Returns_NotFound_For_A_Missing_Article()
    {
        var missingId = Guid.CreateVersion7();
        _articles.Setup(r => r.GetByIdWithDetailsAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KbArticle?)null);

        var result = await CreateSut().GetByIdAsync(missingId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("KNOWLEDGE.ARTICLE_NOT_FOUND", result.Error.Code);
    }
}
