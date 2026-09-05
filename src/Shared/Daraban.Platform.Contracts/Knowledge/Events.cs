namespace Daraban.Platform.Contracts.Knowledge;

/// <summary>Published when a KB article transitions Draft -> Published (Task 6.4).
/// Notifications consumes this to alert subscribers of a new self-service article.</summary>
public sealed record KbArticlePublishedEvent(
    Guid ArticleId,
    Guid EntityId,
    string Title,
    Guid? CategoryId,
    bool IsFaq,
    Guid ActorUserId);

/// <summary>Published when a published article is pulled back to Draft or Archived, so any
/// downstream index/cache/notification consumer can stop surfacing it.</summary>
public sealed record KbArticleUnpublishedEvent(
    Guid ArticleId,
    Guid EntityId,
    string NewStatus,
    Guid ActorUserId);

/// <summary>Published when an article is attached to a ticket. Knowledge owns the link row
/// (knowledge.kb_ticket_links); ServiceDesk cannot be referenced directly from this module
/// (Task 1.1 SS3 -- cross-module traffic goes through Contracts only), so ServiceDesk stamps
/// the ticket's resolution from this event rather than via a direct service call.</summary>
public sealed record KbArticleLinkedToTicketEvent(
    Guid ArticleId,
    Guid TicketId,
    Guid EntityId,
    bool IsSolution,
    Guid ActorUserId);
