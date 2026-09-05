using Daraban.Modules.Knowledge.Data.Entities;
using Daraban.Modules.Knowledge.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Knowledge.Services.Interfaces;

public interface IKbArticleService
{
    Task<Result<KbArticlePagedResult>> GetPagedAsync(
        Guid entityNodeId,
        Guid? categoryId,
        KbArticleStatus? status,
        bool? isFaq,
        Guid? authorUserId,
        string? titleContains,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Reads one article. <paramref name="incrementViewCount"/> is set by the public
    /// read endpoint and left false for author/edit reads so editing doesn't inflate the counter.</summary>
    Task<Result<KbArticleDto>> GetByIdAsync(
        Guid id, bool incrementViewCount = false, CancellationToken ct = default);

    /// <summary>
    /// Full-text search over the tsvector column, ranked by ts_rank (Task 6.4).
    /// </summary>
    Task<Result<KbArticleSearchResult>> SearchAsync(
        Guid entityNodeId,
        string query,
        Guid? categoryId,
        KbArticleStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<KbArticleDto>> CreateAsync(
        CreateKbArticleRequest request, Guid entityNodeId, Guid actorUserId, CancellationToken ct = default);

    Task<Result<KbArticleDto>> UpdateAsync(
        Guid id, UpdateKbArticleRequest request, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Draft -> Published -> Archived transitions, validated against a state machine.
    /// Publishing stamps PublishedAt/PublishedByUserId and emits KbArticlePublishedEvent.</summary>
    Task<Result<KbArticleDto>> ChangeStatusAsync(
        Guid id, KbArticleStatus newStatus, Guid actorUserId, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Upserts the caller's helpful/not-helpful verdict and returns the refreshed counters.</summary>
    Task<Result<KbFeedbackSummaryDto>> SubmitFeedbackAsync(
        Guid articleId, SubmitKbFeedbackRequest request, Guid actorUserId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<KbFeedbackDto>>> GetFeedbackAsync(Guid articleId, CancellationToken ct = default);
}
