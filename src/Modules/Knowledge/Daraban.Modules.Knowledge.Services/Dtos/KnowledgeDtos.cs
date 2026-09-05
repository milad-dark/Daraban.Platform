using Daraban.Modules.Knowledge.Data.Entities;

namespace Daraban.Modules.Knowledge.Services.Dtos;

// ---- Category DTOs ----------------------------------------------------------------------

public record KbCategoryDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Slug,
    string? Description,
    int SortOrder,
    bool IsActive,
    int ArticleCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Nested form returned by GET /api/v1/kb/categories?tree=true -- the whole tree is
/// assembled in one pass in memory from a single flat query, not N queries per level.</summary>
public record KbCategoryTreeDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Slug,
    string? Description,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<KbCategoryTreeDto> Children);

public record CreateKbCategoryRequest(
    Guid? ParentId,
    string Name,
    string? Slug,
    string? Description,
    int SortOrder = 0);

public record UpdateKbCategoryRequest(
    Guid? ParentId,
    string Name,
    string? Slug,
    string? Description,
    bool IsActive,
    int SortOrder = 0);

// ---- Article DTOs -----------------------------------------------------------------------

public record KbArticleDto(
    Guid Id,
    string Title,
    string Content,
    string? Summary,
    Guid? CategoryId,
    string? CategoryName,
    KbArticleStatus Status,
    bool IsFaq,
    Guid AuthorUserId,
    DateTimeOffset? PublishedAt,
    Guid? PublishedByUserId,
    int ViewCount,
    int HelpfulCount,
    int NotHelpfulCount,
    string? Tags,
    IReadOnlyList<KbArticleTargetDto> Targets,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record KbArticleListDto(
    Guid Id,
    string Title,
    string? Summary,
    Guid? CategoryId,
    KbArticleStatus Status,
    bool IsFaq,
    Guid AuthorUserId,
    int ViewCount,
    int HelpfulCount,
    int NotHelpfulCount,
    DateTimeOffset UpdatedAt);

/// <summary>A search result: the list projection plus the ts_rank relevance score that
/// ordered it.</summary>
public record KbArticleSearchHitDto(KbArticleListDto Article, double Rank);

public record KbArticleTargetDto(
    Guid Id,
    KbTargetType TargetType,
    Guid? TargetId,
    bool IsRecursive);

public record KbArticleTargetInput(
    KbTargetType TargetType,
    Guid? TargetId,
    bool IsRecursive = false);

public record CreateKbArticleRequest(
    string Title,
    string Content,
    string? Summary,
    Guid? CategoryId,
    bool IsFaq,
    string? Tags,
    IReadOnlyList<KbArticleTargetInput>? Targets);

public record UpdateKbArticleRequest(
    string Title,
    string Content,
    string? Summary,
    Guid? CategoryId,
    bool IsFaq,
    string? Tags,
    IReadOnlyList<KbArticleTargetInput>? Targets);

/// <summary>Body of POST /api/v1/kb/articles/{id}/status. Archived and Draft need no extra
/// data; Published stamps PublishedAt/PublishedByUserId.</summary>
public record ChangeKbArticleStatusRequest(KbArticleStatus Status);

public record KbArticlePagedResult(
    IReadOnlyList<KbArticleListDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record KbArticleSearchResult(
    IReadOnlyList<KbArticleSearchHitDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    string Query);

// ---- Feedback DTOs ----------------------------------------------------------------------

public record KbFeedbackDto(
    Guid Id,
    Guid ArticleId,
    Guid UserId,
    bool IsHelpful,
    string? Comment,
    DateTimeOffset SubmittedAt);

public record SubmitKbFeedbackRequest(bool IsHelpful, string? Comment);

/// <summary>Returned after submitting feedback so the client can update the counters without
/// a second round trip.</summary>
public record KbFeedbackSummaryDto(
    Guid ArticleId,
    int HelpfulCount,
    int NotHelpfulCount,
    KbFeedbackDto Submitted);

// ---- Ticket link DTOs -------------------------------------------------------------------

public record KbTicketLinkDto(
    Guid Id,
    Guid ArticleId,
    string? ArticleTitle,
    Guid TicketId,
    bool IsSolution,
    Guid LinkedByUserId,
    DateTimeOffset LinkedAt,
    string? Note);

/// <summary>Body of POST /api/v1/tickets/{id}/solution.</summary>
public record LinkKbArticleToTicketRequest(
    Guid ArticleId,
    bool IsSolution,
    string? Note);
