using Daraban.Modules.Knowledge.Data.Entities;
using Daraban.Modules.Knowledge.Data.Repositories;
using Daraban.Modules.Knowledge.Services;
using Daraban.Modules.Knowledge.Services.Dtos;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daraban.Modules.Knowledge.Tests;

/// <summary>
/// Task 8.2 read-model caching: hits, misses, generation-bump invalidation on mutation,
/// and the null-cache escape hatch that keeps the service usable without a cache.
/// Each test picks its own entity id because the generation counters are static.
/// </summary>
public class KbArticleServiceCacheTests
{
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IKbArticleRepository> _articles = new(MockBehavior.Strict);
    private readonly Mock<IKbCategoryRepository> _categories = new(MockBehavior.Strict);
    private readonly Mock<IKbFeedbackRepository> _feedback = new(MockBehavior.Strict);
    private readonly Mock<IEventPublisher> _events = new();
    private readonly MemoryCache _cache = new(Options.Create(new MemoryCacheOptions()));

    private KbArticleService CreateSut() =>
        new(_articles.Object, _categories.Object, _feedback.Object, _events.Object,
            _cache, Options.Create(new KbCacheOptions { TtlSeconds = 60 }));

    private KbArticleService CreateSutWithoutCache() =>
        new(_articles.Object, _categories.Object, _feedback.Object, _events.Object);

    private static KbArticle Article(Guid entityId)
        => new()
        {
            Id = Guid.CreateVersion7(),
            EntityId = entityId,
            Title = "VPN reset",
            Content = "How to reset the VPN client.",
            Status = KbArticleStatus.Published,
            AuthorUserId = ActorId,
            ViewCount = 3,
            HelpfulCount = 1,
            NotHelpfulCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private void SetupPagedOnce(Guid entityId, KbArticle article)
    {
        _articles
            .Setup(r => r.GetPagedAsync(
                entityId, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(((IReadOnlyList<KbArticle>)new[] { article }, 1)));
    }

    // ---- GetPagedAsync --------------------------------------------------------------------

    [Fact]
    public async Task GetPagedAsync_Serves_Second_Identical_Request_From_Cache()
    {
        var entityId = Guid.CreateVersion7();
        var article = Article(entityId);
        SetupPagedOnce(entityId, article);
        var sut = CreateSut();

        var first = await sut.GetPagedAsync(entityId, null, null, null, null, null, 1, 20);
        var second = await sut.GetPagedAsync(entityId, null, null, null, null, null, 1, 20);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Items.Single().Id, second.Value.Items.Single().Id);
        _articles.Verify(r => r.GetPagedAsync(
            entityId, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_Different_Filter_Key_Hits_Repository_Again()
    {
        var entityId = Guid.CreateVersion7();
        var article = Article(entityId);
        SetupPagedOnce(entityId, article);
        _articles
            .Setup(r => r.GetPagedAsync(
                entityId, null, KbArticleStatus.Published, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(((IReadOnlyList<KbArticle>)new[] { article }, 1)));
        var sut = CreateSut();

        await sut.GetPagedAsync(entityId, null, null, null, null, null, 1, 20);
        var other = await sut.GetPagedAsync(entityId, null, KbArticleStatus.Published, null, null, null, 1, 20);

        Assert.True(other.IsSuccess);
        _articles.Verify(r => r.GetPagedAsync(
            entityId, null, It.IsAny<KbArticleStatus?>(), null, null, null, 1, 20,
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetPagedAsync_Without_Cache_Always_Hits_Repository()
    {
        var entityId = Guid.CreateVersion7();
        var article = Article(entityId);
        SetupPagedOnce(entityId, article);
        var sut = CreateSutWithoutCache();

        await sut.GetPagedAsync(entityId, null, null, null, null, null, 1, 20);
        await sut.GetPagedAsync(entityId, null, null, null, null, null, 1, 20);

        _articles.Verify(r => r.GetPagedAsync(
            entityId, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ---- invalidation ---------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_Bumps_Generation_So_Next_List_Read_Requeries()
    {
        var entityId = Guid.CreateVersion7();
        var article = Article(entityId);
        SetupPagedOnce(entityId, article);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _articles.Setup(r => r.Update(article));
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _articles.Setup(r => r.GetByIdWithDetailsAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);
        var sut = CreateSut();

        await sut.GetPagedAsync(entityId, null, null, null, null, null, 1, 20);

        var request = new UpdateKbArticleRequest("Renamed", "Body", null, null, false, null, Targets: null);
        var updated = await sut.UpdateAsync(article.Id, request, ActorId);
        Assert.True(updated.IsSuccess);

        var after = await sut.GetPagedAsync(entityId, null, null, null, null, null, 1, 20);
        Assert.True(after.IsSuccess);
        _articles.Verify(r => r.GetPagedAsync(
            entityId, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateAsync_Invalidate_Scopes_To_Owning_Entity()
    {
        var entityId = Guid.CreateVersion7();
        var otherEntityId = Guid.CreateVersion7();
        var article = Article(entityId);
        var other = Article(otherEntityId);
        SetupPagedOnce(entityId, article);
        _articles
            .Setup(r => r.GetPagedAsync(
                otherEntityId, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(((IReadOnlyList<KbArticle>)new[] { other }, 1)));
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _articles.Setup(r => r.Update(article));
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _articles.Setup(r => r.GetByIdWithDetailsAsync(article.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(article);
        var sut = CreateSut();

        await sut.GetPagedAsync(otherEntityId, null, null, null, null, null, 1, 20);

        var request = new UpdateKbArticleRequest("Renamed", "Body", null, null, false, null, Targets: null);
        Assert.True((await sut.UpdateAsync(article.Id, request, ActorId)).IsSuccess);

        // The untouched entity's cached page must still be served from cache.
        Assert.True((await sut.GetPagedAsync(otherEntityId, null, null, null, null, null, 1, 20)).IsSuccess);
        _articles.Verify(r => r.GetPagedAsync(
            otherEntityId, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Bumps_Generation_So_Next_List_Read_Requeries()
    {
        var entityId = Guid.CreateVersion7();
        var article = Article(entityId);
        SetupPagedOnce(entityId, article);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _articles.Setup(r => r.Update(article));
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.GetPagedAsync(entityId, null, null, null, null, null, 1, 20);
        Assert.True((await sut.DeleteAsync(article.Id, ActorId)).IsSuccess);
        var after = await sut.GetPagedAsync(entityId, null, null, null, null, null, 1, 20);

        Assert.True(after.IsSuccess);
        _articles.Verify(r => r.GetPagedAsync(
            entityId, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ChangeStatusAsync_Bumps_Generation_So_Next_List_Read_Requeries()
    {
        var entityId = Guid.CreateVersion7();
        var article = Article(entityId); // Published
        SetupPagedOnce(entityId, article);
        _articles.Setup(r => r.GetByIdAsync(article.Id, It.IsAny<CancellationToken>())).ReturnsAsync(article);
        _articles.Setup(r => r.Update(article));
        _articles.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.GetPagedAsync(entityId, null, null, null, null, null, 1, 20);
        var archived = await sut.ChangeStatusAsync(article.Id, KbArticleStatus.Archived, ActorId);
        Assert.True(archived.IsSuccess);
        var after = await sut.GetPagedAsync(entityId, null, null, null, null, null, 1, 20);

        Assert.True(after.IsSuccess);
        _articles.Verify(r => r.GetPagedAsync(
            entityId, null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ---- SearchAsync ----------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_Serves_Second_Identical_Search_From_Cache()
    {
        var entityId = Guid.CreateVersion7();
        var article = Article(entityId);
        _articles
            .Setup(r => r.SearchAsync(
                entityId, "vpn", null, null, 1, 20, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(((IReadOnlyList<KbSearchHit>)new[] { new KbSearchHit(article, 0.9) }, 1)));
        var sut = CreateSut();

        var first = await sut.SearchAsync(entityId, "vpn", null, null, 1, 20);
        var second = await sut.SearchAsync(entityId, "vpn", null, null, 1, 20);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(0.9, second.Value.Items.Single().Rank);
        _articles.Verify(r => r.SearchAsync(
            entityId, "vpn", null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }
}
