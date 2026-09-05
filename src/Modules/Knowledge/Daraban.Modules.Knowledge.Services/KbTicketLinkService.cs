using Daraban.Modules.Knowledge.Data.Entities;
using Daraban.Modules.Knowledge.Data.Repositories;
using Daraban.Modules.Knowledge.Services.Dtos;
using Daraban.Modules.Knowledge.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Contracts.Knowledge;

namespace Daraban.Modules.Knowledge.Services;

public class KbTicketLinkService : IKbTicketLinkService
{
    private readonly IKbTicketLinkRepository _links;
    private readonly IKbArticleRepository _articles;
    private readonly IEventPublisher _events;

    public KbTicketLinkService(
        IKbTicketLinkRepository links,
        IKbArticleRepository articles,
        IEventPublisher events)
    {
        _links = links;
        _articles = articles;
        _events = events;
    }

    public async Task<Result<IReadOnlyList<KbTicketLinkDto>>> GetByTicketAsync(
        Guid ticketId, CancellationToken ct = default)
    {
        var links = await _links.GetByTicketAsync(ticketId, ct);
        var dtos = links.Select(l => MapToDto(l)).ToList();
        return Result.Success<IReadOnlyList<KbTicketLinkDto>>(dtos);
    }

    public async Task<Result<KbTicketLinkDto>> LinkAsync(
        Guid ticketId,
        LinkKbArticleToTicketRequest request,
        Guid entityNodeId,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var article = await _articles.GetByIdAsync(request.ArticleId, ct);
        if (article is null)
            return Result.Failure<KbTicketLinkDto>(new Error(
                "KNOWLEDGE.ARTICLE_NOT_FOUND", "Article not found.", ErrorType.NotFound));

        // The caller's active entity must own the article. Without this, any ticket could cite an
        // article belonging to a different tenant.
        if (article.EntityId != entityNodeId)
            return Result.Failure<KbTicketLinkDto>(new Error(
                "KNOWLEDGE.ARTICLE_CROSS_ENTITY",
                "Article belongs to a different entity.", ErrorType.Forbidden));

        // A draft or archived article is not a resolution anyone can read.
        if (request.IsSolution && article.Status != KbArticleStatus.Published)
            return Result.Failure<KbTicketLinkDto>(new Error(
                "KNOWLEDGE.ARTICLE_NOT_PUBLISHED",
                "Only a published article can be recorded as a ticket solution.", ErrorType.BusinessRule));

        var now = DateTimeOffset.UtcNow;

        // Demote the incumbent solution before promoting this one -- otherwise
        // uq_kb_ticket_links_ticket_solution rejects the write.
        if (request.IsSolution)
        {
            var currentSolution = await _links.GetSolutionAsync(ticketId, ct);
            if (currentSolution is not null && currentSolution.ArticleId != request.ArticleId)
            {
                currentSolution.IsSolution = false;
                currentSolution.UpdatedAt = now;
                currentSolution.UpdatedById = actorUserId;
                _links.Update(currentSolution);
            }
        }

        // Re-linking the same article is an update, not a duplicate row
        // (uq_kb_ticket_links_ticket_article).
        var existing = await _links.GetAsync(ticketId, request.ArticleId, ct);
        KbTicketLink link;

        if (existing is null)
        {
            link = new KbTicketLink
            {
                Id = Guid.CreateVersion7(),
                ArticleId = request.ArticleId,
                TicketId = ticketId,
                IsSolution = request.IsSolution,
                LinkedByUserId = actorUserId,
                LinkedAt = now,
                Note = request.Note,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedById = actorUserId,
                UpdatedById = actorUserId,
            };
            await _links.AddAsync(link, ct);
        }
        else
        {
            existing.IsSolution = request.IsSolution;
            existing.Note = request.Note;
            existing.LinkedByUserId = actorUserId;
            existing.LinkedAt = now;
            existing.UpdatedAt = now;
            existing.UpdatedById = actorUserId;
            _links.Update(existing);
            link = existing;
        }

        await _links.SaveChangesAsync(ct);

        // ServiceDesk stamps the ticket's resolution from this event. Knowledge cannot call
        // ITicketService directly -- cross-module traffic goes through Contracts (Task 1.1 SS3).
        await _events.PublishAsync(new KbArticleLinkedToTicketEvent(
            link.ArticleId, link.TicketId, entityNodeId, link.IsSolution, actorUserId), ct);

        return Result.Success(MapToDto(link, article.Title));
    }

    public async Task<Result> UnlinkAsync(Guid ticketId, Guid articleId, CancellationToken ct = default)
    {
        var link = await _links.GetAsync(ticketId, articleId, ct);
        if (link is null)
            return Result.Failure(new Error(
                "KNOWLEDGE.TICKET_LINK_NOT_FOUND", "Ticket link not found.", ErrorType.NotFound));

        // Hard delete: this is a join row with no history value of its own, and the ticket's own
        // audit trail (TicketHistory) already records that the solution was removed.
        _links.Remove(link);
        await _links.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static KbTicketLinkDto MapToDto(KbTicketLink l, string? articleTitleOverride = null) => new(
        l.Id,
        l.ArticleId,
        l.Article?.Title ?? articleTitleOverride,
        l.TicketId,
        l.IsSolution,
        l.LinkedByUserId,
        l.LinkedAt,
        l.Note);
}
