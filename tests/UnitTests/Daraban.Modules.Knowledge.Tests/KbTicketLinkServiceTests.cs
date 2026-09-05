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
/// KbTicketLinkService: the one-solution-per-ticket rule, the cross-entity guard, and the fact
/// that ServiceDesk is notified by event rather than by a direct call across the module
/// boundary (Task 1.1 SS3).
/// </summary>
public class KbTicketLinkServiceTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TicketId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly Mock<IKbTicketLinkRepository> _links = new(MockBehavior.Strict);
    private readonly Mock<IKbArticleRepository> _articles = new(MockBehavior.Strict);
    private readonly Mock<IEventPublisher> _events = new();

    private KbTicketLinkService CreateSut() => new(_links.Object, _articles.Object, _events.Object);

    private static KbArticle PublishedArticle(Guid? entityId = null) => new()
    {
        Id = Guid.CreateVersion7(),
        EntityId = entityId ?? EntityId,
        Title = "VPN reset",
        Content = "Steps...",
        Status = KbArticleStatus.Published,
    };

    [Fact]
    public async Task LinkAsync_Returns_NotFound_For_A_Missing_Article()
    {
        var articleId = Guid.CreateVersion7();
        _articles.Setup(r => r.GetByIdAsync(articleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KbArticle?)null);

        var result = await CreateSut().LinkAsync(
            TicketId, new LinkKbArticleToTicketRequest(articleId, true, null), EntityId, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.ARTICLE_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task LinkAsync_Refuses_An_Article_Belonging_To_Another_Entity()
    {
        var foreign = PublishedArticle(entityId: Guid.CreateVersion7());
        _articles.Setup(r => r.GetByIdAsync(foreign.Id, It.IsAny<CancellationToken>())).ReturnsAsync(foreign);

        var result = await CreateSut().LinkAsync(
            TicketId, new LinkKbArticleToTicketRequest(foreign.Id, true, null), EntityId, ActorId);

        // Without this a ticket could cite another tenant's article.
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Equal("KNOWLEDGE.ARTICLE_CROSS_ENTITY", result.Error.Code);
    }

    [Theory]
    [InlineData(KbArticleStatus.Draft)]
    [InlineData(KbArticleStatus.Archived)]
    public async Task LinkAsync_Refuses_An_Unpublished_Article_As_Solution(KbArticleStatus status)
    {
        var article = PublishedArticle();
        article.Status = status;
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);

        var result = await CreateSut().LinkAsync(
            TicketId, new LinkKbArticleToTicketRequest(article.Id, IsSolution: true, null), EntityId, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.ARTICLE_NOT_PUBLISHED", result.Error!.Code);
    }

    [Fact]
    public async Task LinkAsync_Allows_A_Draft_As_A_Non_Solution_Reference()
    {
        var article = PublishedArticle();
        article.Status = KbArticleStatus.Draft;

        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _links.Setup(r => r.GetAsync(TicketId, article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KbTicketLink?)null);
        _links.Setup(r => r.AddAsync(It.IsAny<KbTicketLink>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _links.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().LinkAsync(
            TicketId, new LinkKbArticleToTicketRequest(article.Id, IsSolution: false, "related"), EntityId, ActorId);

        // Only the *solution* claim requires publication -- a mere reference does not.
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsSolution);
    }

    [Fact]
    public async Task LinkAsync_Demotes_The_Previous_Solution_Before_Promoting_The_New_One()
    {
        var article = PublishedArticle();
        var incumbent = new KbTicketLink
        {
            Id = Guid.CreateVersion7(),
            TicketId = TicketId,
            ArticleId = Guid.CreateVersion7(),
            IsSolution = true,
        };

        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _links.Setup(r => r.GetSolutionAsync(TicketId, It.IsAny<CancellationToken>())).ReturnsAsync(incumbent);
        _links.Setup(r => r.Update(incumbent));
        _links.Setup(r => r.GetAsync(TicketId, article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KbTicketLink?)null);
        _links.Setup(r => r.AddAsync(It.IsAny<KbTicketLink>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _links.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().LinkAsync(
            TicketId, new LinkKbArticleToTicketRequest(article.Id, true, null), EntityId, ActorId);

        Assert.True(result.IsSuccess);

        // uq_kb_ticket_links_ticket_solution would otherwise reject the insert.
        Assert.False(incumbent.IsSolution);
        Assert.True(result.Value.IsSolution);
    }

    [Fact]
    public async Task LinkAsync_Relinking_The_Same_Article_Updates_Instead_Of_Duplicating()
    {
        var article = PublishedArticle();
        var existing = new KbTicketLink
        {
            Id = Guid.CreateVersion7(),
            TicketId = TicketId,
            ArticleId = article.Id,
            IsSolution = false,
            Note = "old note",
        };

        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _links.Setup(r => r.GetSolutionAsync(TicketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KbTicketLink?)null);
        _links.Setup(r => r.GetAsync(TicketId, article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _links.Setup(r => r.Update(existing));
        _links.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().LinkAsync(
            TicketId, new LinkKbArticleToTicketRequest(article.Id, true, "new note"), EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(existing.IsSolution);
        Assert.Equal("new note", existing.Note);

        // uq_kb_ticket_links_ticket_article -- one row per (ticket, article) pair.
        _links.Verify(r => r.AddAsync(It.IsAny<KbTicketLink>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LinkAsync_Does_Not_Demote_When_Relinking_The_Existing_Solution_Article()
    {
        var article = PublishedArticle();
        var sameArticleSolution = new KbTicketLink
        {
            Id = Guid.CreateVersion7(),
            TicketId = TicketId,
            ArticleId = article.Id,
            IsSolution = true,
        };

        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _links.Setup(r => r.GetSolutionAsync(TicketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sameArticleSolution);
        _links.Setup(r => r.GetAsync(TicketId, article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sameArticleSolution);
        _links.Setup(r => r.Update(sameArticleSolution));
        _links.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().LinkAsync(
            TicketId, new LinkKbArticleToTicketRequest(article.Id, true, "refreshed"), EntityId, ActorId);

        Assert.True(result.IsSuccess);
        // Demoting then re-promoting the same row would be a pointless write, and briefly leave
        // the ticket with no solution.
        Assert.True(sameArticleSolution.IsSolution);
    }

    [Fact]
    public async Task LinkAsync_Notifies_ServiceDesk_By_Event_Not_By_Direct_Call()
    {
        var article = PublishedArticle();
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _links.Setup(r => r.GetSolutionAsync(TicketId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KbTicketLink?)null);
        _links.Setup(r => r.GetAsync(TicketId, article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KbTicketLink?)null);
        _links.Setup(r => r.AddAsync(It.IsAny<KbTicketLink>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _links.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().LinkAsync(
            TicketId, new LinkKbArticleToTicketRequest(article.Id, true, null), EntityId, ActorId);

        _events.Verify(e => e.PublishAsync(
            It.Is<KbArticleLinkedToTicketEvent>(evt =>
                evt.TicketId == TicketId &&
                evt.ArticleId == article.Id &&
                evt.IsSolution &&
                evt.ActorUserId == ActorId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnlinkAsync_Returns_NotFound_When_No_Such_Link_Exists()
    {
        var articleId = Guid.CreateVersion7();
        _links.Setup(r => r.GetAsync(TicketId, articleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KbTicketLink?)null);

        var result = await CreateSut().UnlinkAsync(TicketId, articleId);

        Assert.False(result.IsSuccess);
        Assert.Equal("KNOWLEDGE.TICKET_LINK_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task UnlinkAsync_Removes_The_Join_Row()
    {
        var link = new KbTicketLink
        {
            Id = Guid.CreateVersion7(),
            TicketId = TicketId,
            ArticleId = Guid.CreateVersion7(),
        };

        _links.Setup(r => r.GetAsync(TicketId, link.ArticleId, It.IsAny<CancellationToken>())).ReturnsAsync(link);
        _links.Setup(r => r.Remove(link));
        _links.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UnlinkAsync(TicketId, link.ArticleId);

        Assert.True(result.IsSuccess);
        _links.Verify(r => r.Remove(link), Times.Once);
    }
}
