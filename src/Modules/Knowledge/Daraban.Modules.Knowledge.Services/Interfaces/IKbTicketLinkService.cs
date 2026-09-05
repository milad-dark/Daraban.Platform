using Daraban.Modules.Knowledge.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Knowledge.Services.Interfaces;

/// <summary>
/// Owns the article-to-ticket relationship (Task 6.4: POST /api/v1/tickets/{id}/solution).
/// Knowledge owns the link table; ServiceDesk is notified via KbArticleLinkedToTicketEvent
/// rather than being called directly, because a Knowledge -> ServiceDesk project reference
/// would break the module-boundary rule (Task 1.1 SS3).
/// </summary>
public interface IKbTicketLinkService
{
    Task<Result<IReadOnlyList<KbTicketLinkDto>>> GetByTicketAsync(Guid ticketId, CancellationToken ct = default);

    /// <summary>Links an article to a ticket. When IsSolution is true, any existing solution
    /// link for that ticket is demoted first so the one-solution-per-ticket unique index holds.</summary>
    Task<Result<KbTicketLinkDto>> LinkAsync(
        Guid ticketId, LinkKbArticleToTicketRequest request, Guid entityNodeId, Guid actorUserId, CancellationToken ct = default);

    Task<Result> UnlinkAsync(Guid ticketId, Guid articleId, CancellationToken ct = default);
}
