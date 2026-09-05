using Daraban.Modules.Knowledge.Data.Entities;
using Daraban.Modules.Knowledge.Data.Repositories;
using Daraban.Modules.Knowledge.Services.Dtos;
using Daraban.Modules.Knowledge.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Contracts.Knowledge;

namespace Daraban.Modules.Knowledge.Services;

public class KbArticleService : IKbArticleService
{
    private readonly IKbArticleRepository _articles;
    private readonly IKbCategoryRepository _categories;
    private readonly IKbFeedbackRepository _feedback;
    private readonly IEventPublisher _events;

    public KbArticleService(
        IKbArticleRepository articles,
        IKbCategoryRepository categories,
        IKbFeedbackRepository feedback,
        IEventPublisher events)
    {
        _articles = articles;
        _categories = categories;
        _feedback = feedback;
        _events = events;
    }

    public async Task<Result<KbArticlePagedResult>> GetPagedAsync(
        Guid entityNodeId,
        Guid? categoryId,
        KbArticleStatus? status,
        bool? isFaq,
        Guid? authorUserId,
        string? titleContains,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);

        var (items, totalCount) = await _articles.GetPagedAsync(
            entityNodeId, categoryId, status, isFaq, authorUserId, titleContains,
            normalizedPage, normalizedPageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result.Success(new KbArticlePagedResult(dtos, totalCount, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<KbArticleDto>> GetByIdAsync(
        Guid id, bool incrementViewCount = false, CancellationToken ct = default)
    {
        var article = await _articles.GetByIdWithDetailsAsync(id, ct);
        if (article is null)
            return Result.Failure<KbArticleDto>(ArticleNotFound());

        if (incrementViewCount)
        {
            await _articles.IncrementViewCountAsync(id, ct);
            // ExecuteUpdateAsync bypasses the change tracker, so the loaded instance still holds
            // the pre-increment value. Reflect it locally so the response matches the stored row.
            article.ViewCount += 1;
        }

        return Result.Success(MapToDto(article));
    }

    public async Task<Result<KbArticleSearchResult>> SearchAsync(
        Guid entityNodeId,
        string query,
        Guid? categoryId,
        KbArticleStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Result.Failure<KbArticleSearchResult>(new Error(
                "KNOWLEDGE.SEARCH_QUERY_REQUIRED", "A search query is required.", ErrorType.Validation));

        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);
        var trimmed = query.Trim();

        var (hits, totalCount) = await _articles.SearchAsync(
            entityNodeId, trimmed, categoryId, status, normalizedPage, normalizedPageSize, ct);

        var dtos = hits.Select(h => new KbArticleSearchHitDto(MapToListDto(h.Article), h.Rank)).ToList();
        return Result.Success(new KbArticleSearchResult(
            dtos, totalCount, normalizedPage, normalizedPageSize, trimmed));
    }

    public async Task<Result<KbArticleDto>> CreateAsync(
        CreateKbArticleRequest request, Guid entityNodeId, Guid actorUserId, CancellationToken ct = default)
    {
        var categoryCheck = await ValidateCategoryAsync(request.CategoryId, entityNodeId, ct);
        if (!categoryCheck.IsSuccess)
            return Result.Failure<KbArticleDto>(categoryCheck.Error!);

        var targetsCheck = ValidateTargets(request.Targets);
        if (!targetsCheck.IsSuccess)
            return Result.Failure<KbArticleDto>(targetsCheck.Error!);

        var now = DateTimeOffset.UtcNow;
        var articleId = Guid.CreateVersion7();

        var article = new KbArticle
        {
            Id = articleId,
            EntityId = entityNodeId,
            Title = request.Title,
            Content = request.Content,
            Summary = request.Summary,
            CategoryId = request.CategoryId,
            // Always born as a Draft. Publishing is a separate, explicit transition so an
            // article can never go live as a side effect of being created.
            Status = KbArticleStatus.Draft,
            IsFaq = request.IsFaq,
            AuthorUserId = actorUserId,
            Tags = NormalizeTags(request.Tags),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
            Targets = BuildTargets(articleId, request.Targets, actorUserId, now),
        };

        await _articles.AddAsync(article, ct);
        await _articles.SaveChangesAsync(ct);

        var categoryName = await ResolveCategoryNameAsync(article.CategoryId, ct);
        return Result.Success(MapToDto(article, categoryName));
    }

    public async Task<Result<KbArticleDto>> UpdateAsync(
        Guid id, UpdateKbArticleRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var article = await _articles.GetByIdAsync(id, ct);
        if (article is null)
            return Result.Failure<KbArticleDto>(ArticleNotFound());

        // Archived is a terminal state for edits -- restore it to Draft first.
        if (article.Status == KbArticleStatus.Archived)
            return Result.Failure<KbArticleDto>(new Error(
                "KNOWLEDGE.ARTICLE_ARCHIVED",
                "Cannot edit an archived article. Restore it to draft first.", ErrorType.BusinessRule));

        var categoryCheck = await ValidateCategoryAsync(request.CategoryId, article.EntityId, ct);
        if (!categoryCheck.IsSuccess)
            return Result.Failure<KbArticleDto>(categoryCheck.Error!);

        var targetsCheck = ValidateTargets(request.Targets);
        if (!targetsCheck.IsSuccess)
            return Result.Failure<KbArticleDto>(targetsCheck.Error!);

        var now = DateTimeOffset.UtcNow;

        article.Title = request.Title;
        article.Content = request.Content;
        article.Summary = request.Summary;
        article.CategoryId = request.CategoryId;
        article.IsFaq = request.IsFaq;
        article.Tags = NormalizeTags(request.Tags);
        article.UpdatedAt = now;
        article.UpdatedById = actorUserId;

        _articles.Update(article);

        // Null Targets means "leave targeting alone"; an empty list means "clear it".
        if (request.Targets is not null)
            await _articles.ReplaceTargetsAsync(
                id, BuildTargets(id, request.Targets, actorUserId, now), ct);

        await _articles.SaveChangesAsync(ct);

        var refreshed = await _articles.GetByIdWithDetailsAsync(id, ct);
        return Result.Success(MapToDto(refreshed ?? article));
    }

    public async Task<Result<KbArticleDto>> ChangeStatusAsync(
        Guid id, KbArticleStatus newStatus, Guid actorUserId, CancellationToken ct = default)
    {
        var article = await _articles.GetByIdAsync(id, ct);
        if (article is null)
            return Result.Failure<KbArticleDto>(ArticleNotFound());

        if (article.Status == newStatus)
            return Result.Failure<KbArticleDto>(new Error(
                "KNOWLEDGE.ARTICLE_STATUS_UNCHANGED",
                $"Article is already {newStatus}.", ErrorType.BusinessRule));

        if (!IsTransitionAllowed(article.Status, newStatus))
            return Result.Failure<KbArticleDto>(new Error(
                "KNOWLEDGE.ARTICLE_INVALID_TRANSITION",
                $"Cannot transition an article from {article.Status} to {newStatus}.", ErrorType.BusinessRule));

        // An empty body may sit in a draft while it's being written, but must not go live.
        if (newStatus == KbArticleStatus.Published && string.IsNullOrWhiteSpace(article.Content))
            return Result.Failure<KbArticleDto>(new Error(
                "KNOWLEDGE.ARTICLE_EMPTY_CONTENT",
                "Cannot publish an article with empty content.", ErrorType.BusinessRule));

        var previousStatus = article.Status;
        var now = DateTimeOffset.UtcNow;

        article.Status = newStatus;
        article.UpdatedAt = now;
        article.UpdatedById = actorUserId;

        if (newStatus == KbArticleStatus.Published)
        {
            article.PublishedAt = now;
            article.PublishedByUserId = actorUserId;
        }

        _articles.Update(article);
        await _articles.SaveChangesAsync(ct);

        // Publish after the commit -- the database row is the source of truth, and emitting
        // first would let a failed save leave subscribers believing in an article that isn't live.
        if (newStatus == KbArticleStatus.Published)
        {
            await _events.PublishAsync(new KbArticlePublishedEvent(
                article.Id, article.EntityId, article.Title, article.CategoryId, article.IsFaq, actorUserId), ct);
        }
        else if (previousStatus == KbArticleStatus.Published)
        {
            await _events.PublishAsync(new KbArticleUnpublishedEvent(
                article.Id, article.EntityId, newStatus.ToString(), actorUserId), ct);
        }

        var categoryName = await ResolveCategoryNameAsync(article.CategoryId, ct);
        return Result.Success(MapToDto(article, categoryName));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var article = await _articles.GetByIdAsync(id, ct);
        if (article is null)
            return Result.Failure(ArticleNotFound());

        var now = DateTimeOffset.UtcNow;
        var wasPublished = article.Status == KbArticleStatus.Published;

        article.IsDeleted = true;
        article.DeletedAt = now;
        article.UpdatedAt = now;
        article.UpdatedById = actorUserId;

        _articles.Update(article);
        await _articles.SaveChangesAsync(ct);

        // A soft-deleted article disappears from the portal exactly like an unpublished one, so
        // downstream consumers need the same signal.
        if (wasPublished)
        {
            await _events.PublishAsync(new KbArticleUnpublishedEvent(
                article.Id, article.EntityId, "Deleted", actorUserId), ct);
        }

        return Result.Success();
    }

    public async Task<Result<KbFeedbackSummaryDto>> SubmitFeedbackAsync(
        Guid articleId, SubmitKbFeedbackRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var article = await _articles.GetByIdAsync(articleId, ct);
        if (article is null)
            return Result.Failure<KbFeedbackSummaryDto>(ArticleNotFound());

        // Feedback is about published guidance. Rating a draft would skew the helpfulness
        // counters of something readers can't even reach.
        if (article.Status != KbArticleStatus.Published)
            return Result.Failure<KbFeedbackSummaryDto>(new Error(
                "KNOWLEDGE.ARTICLE_NOT_PUBLISHED",
                "Feedback can only be submitted for published articles.", ErrorType.BusinessRule));

        var now = DateTimeOffset.UtcNow;

        // Upsert: one verdict per user per article (uq_kb_feedback_article_user). A user
        // changing their mind revises their row rather than adding a second vote.
        var existing = await _feedback.GetByArticleAndUserAsync(articleId, actorUserId, ct);
        KbFeedback entry;

        if (existing is null)
        {
            entry = new KbFeedback
            {
                Id = Guid.CreateVersion7(),
                ArticleId = articleId,
                UserId = actorUserId,
                IsHelpful = request.IsHelpful,
                Comment = request.Comment,
                SubmittedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedById = actorUserId,
                UpdatedById = actorUserId,
            };
            await _feedback.AddAsync(entry, ct);
        }
        else
        {
            existing.IsHelpful = request.IsHelpful;
            existing.Comment = request.Comment;
            existing.SubmittedAt = now;
            existing.UpdatedAt = now;
            existing.UpdatedById = actorUserId;
            _feedback.Update(existing);
            entry = existing;
        }

        await _feedback.SaveChangesAsync(ct);

        // Recount from the rows instead of doing +1/-1 arithmetic: the denormalised counters
        // then cannot drift away from the underlying feedback, including on a flipped verdict.
        var (helpful, notHelpful) = await _feedback.CountVerdictsAsync(articleId, ct);
        article.HelpfulCount = helpful;
        article.NotHelpfulCount = notHelpful;
        article.UpdatedAt = now;
        _articles.Update(article);
        await _articles.SaveChangesAsync(ct);

        return Result.Success(new KbFeedbackSummaryDto(
            articleId, helpful, notHelpful, MapToFeedbackDto(entry)));
    }

    public async Task<Result<IReadOnlyList<KbFeedbackDto>>> GetFeedbackAsync(
        Guid articleId, CancellationToken ct = default)
    {
        if (!await _articles.ExistsAsync(articleId, ct))
            return Result.Failure<IReadOnlyList<KbFeedbackDto>>(ArticleNotFound());

        var entries = await _feedback.GetByArticleAsync(articleId, ct);
        var dtos = entries.Select(MapToFeedbackDto).ToList();
        return Result.Success<IReadOnlyList<KbFeedbackDto>>(dtos);
    }

    // ---- validation helpers ---------------------------------------------------------------

    /// <summary>
    /// Draft &lt;-&gt; Published, either to Archived, Archived back to Draft only. Archived is
    /// never re-published directly -- it goes back through Draft so the content gets a look
    /// before it's live again.
    /// </summary>
    internal static bool IsTransitionAllowed(KbArticleStatus from, KbArticleStatus to)
        => (from, to) switch
        {
            (KbArticleStatus.Draft, KbArticleStatus.Published) => true,
            (KbArticleStatus.Draft, KbArticleStatus.Archived) => true,
            (KbArticleStatus.Published, KbArticleStatus.Draft) => true,
            (KbArticleStatus.Published, KbArticleStatus.Archived) => true,
            (KbArticleStatus.Archived, KbArticleStatus.Draft) => true,
            _ => false,
        };

    private async Task<Result> ValidateCategoryAsync(Guid? categoryId, Guid entityNodeId, CancellationToken ct)
    {
        if (categoryId is null)
            return Result.Success();

        var category = await _categories.GetByIdAsync(categoryId.Value, ct);
        if (category is null)
            return Result.Failure(new Error(
                "KNOWLEDGE.CATEGORY_NOT_FOUND", "Category not found.", ErrorType.NotFound));

        if (category.EntityId != entityNodeId)
            return Result.Failure(new Error(
                "KNOWLEDGE.CATEGORY_CROSS_ENTITY",
                "Category belongs to a different entity.", ErrorType.BusinessRule));

        return Result.Success();
    }

    private static Result ValidateTargets(IReadOnlyList<KbArticleTargetInput>? targets)
    {
        if (targets is null || targets.Count == 0)
            return Result.Success();

        foreach (var target in targets)
        {
            // TargetType.All means "everyone in this entity" and carries no id; every other
            // type is meaningless without one.
            if (target.TargetType == KbTargetType.All)
            {
                if (target.TargetId is not null)
                    return Result.Failure(new Error(
                        "KNOWLEDGE.TARGET_ID_NOT_ALLOWED",
                        "A target of type 'All' must not specify a target id.", ErrorType.Validation));
            }
            else if (target.TargetId is null || target.TargetId == Guid.Empty)
            {
                return Result.Failure(new Error(
                    "KNOWLEDGE.TARGET_ID_REQUIRED",
                    $"A target of type '{target.TargetType}' requires a target id.", ErrorType.Validation));
            }
        }

        // uq_kb_article_targets_article_target would reject these at the database with an opaque
        // constraint violation; catching it here returns a usable message instead.
        var duplicate = targets
            .GroupBy(t => (t.TargetType, t.TargetId))
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
            return Result.Failure(new Error(
                "KNOWLEDGE.TARGET_DUPLICATE",
                $"Duplicate target: {duplicate.Key.TargetType} {duplicate.Key.TargetId}.", ErrorType.Validation));

        return Result.Success();
    }

    private static List<KbArticleTarget> BuildTargets(
        Guid articleId, IReadOnlyList<KbArticleTargetInput>? inputs, Guid actorUserId, DateTimeOffset now)
        => inputs is null
            ? new List<KbArticleTarget>()
            : inputs.Select(i => new KbArticleTarget
            {
                Id = Guid.CreateVersion7(),
                ArticleId = articleId,
                TargetType = i.TargetType,
                TargetId = i.TargetType == KbTargetType.All ? null : i.TargetId,
                // Recursion only means anything for an entity-node target.
                IsRecursive = i.TargetType == KbTargetType.Entity && i.IsRecursive,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedById = actorUserId,
                UpdatedById = actorUserId,
            }).ToList();

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
        => (page < 1 ? 1 : page, pageSize switch { < 1 => 20, > 200 => 200, _ => pageSize });

    /// <summary>Collapses whitespace around comma-separated tags and drops empties, so
    /// "vpn, , Wifi " stores as "vpn,Wifi" rather than three ragged values.</summary>
    private static string? NormalizeTags(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return null;

        var parts = tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : string.Join(',', parts);
    }

    private async Task<string?> ResolveCategoryNameAsync(Guid? categoryId, CancellationToken ct)
        => categoryId is null ? null : (await _categories.GetByIdAsync(categoryId.Value, ct))?.Name;

    private static Error ArticleNotFound()
        => new("KNOWLEDGE.ARTICLE_NOT_FOUND", "Article not found.", ErrorType.NotFound);

    // ---- mapping --------------------------------------------------------------------------

    private static KbArticleDto MapToDto(KbArticle a, string? categoryNameOverride = null) => new(
        a.Id,
        a.Title,
        a.Content,
        a.Summary,
        a.CategoryId,
        a.Category?.Name ?? categoryNameOverride,
        a.Status,
        a.IsFaq,
        a.AuthorUserId,
        a.PublishedAt,
        a.PublishedByUserId,
        a.ViewCount,
        a.HelpfulCount,
        a.NotHelpfulCount,
        a.Tags,
        a.Targets.Select(t => new KbArticleTargetDto(t.Id, t.TargetType, t.TargetId, t.IsRecursive)).ToList(),
        a.CreatedAt,
        a.UpdatedAt);

    private static KbArticleListDto MapToListDto(KbArticle a) => new(
        a.Id,
        a.Title,
        a.Summary,
        a.CategoryId,
        a.Status,
        a.IsFaq,
        a.AuthorUserId,
        a.ViewCount,
        a.HelpfulCount,
        a.NotHelpfulCount,
        a.UpdatedAt);

    private static KbFeedbackDto MapToFeedbackDto(KbFeedback f) => new(
        f.Id, f.ArticleId, f.UserId, f.IsHelpful, f.Comment, f.SubmittedAt);
}
