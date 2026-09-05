using Daraban.Modules.Knowledge.Services.Dtos;
using Daraban.Modules.Knowledge.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Hosting;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Knowledge.Api.Controllers;

/// <summary>
/// POST /api/v1/tickets/{id}/solution (Task 6.4) — links a KB article to a ticket as its
/// resolution.
///
/// This route lives under /api/v1/tickets/* but is served by the Knowledge module, because
/// Knowledge owns the kb_ticket_links table. The alternative — putting it in
/// ServiceDesk.Api — would need a ServiceDesk -> Knowledge project reference, which the
/// module-boundary rule forbids (Task 1.1 SS3). ServiceDesk learns about the link by
/// consuming KbArticleLinkedToTicketEvent instead.
/// </summary>
[ApiController]
[Route("api/v1/tickets/{ticketId:guid}")]
[Authorize]
public class KbTicketSolutionsController : ControllerBase
{
    private readonly IKbTicketLinkService _links;
    private readonly ICurrentUser _currentUser;

    public KbTicketSolutionsController(IKbTicketLinkService links, ICurrentUser currentUser)
    {
        _links = links;
        _currentUser = currentUser;
    }

    /// <summary>Every KB article linked to this ticket, accepted solution first.</summary>
    [HttpGet("kb-links")]
    [RequirePermission("servicedesk.read")]
    public async Task<IActionResult> GetLinks(Guid ticketId, CancellationToken ct)
    {
        var result = await _links.GetByTicketAsync(ticketId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>
    /// Records a KB article against the ticket. IsSolution = true marks it as the accepted
    /// resolution and demotes any previous one.
    /// Requires servicedesk.write (this mutates a ticket's resolution), not knowledge.write.
    /// </summary>
    [HttpPost("solution")]
    [RequirePermission("servicedesk.write")]
    public async Task<IActionResult> LinkSolution(
        Guid ticketId, [FromBody] LinkKbArticleToTicketRequest request, CancellationToken ct)
    {
        var result = await _links.LinkAsync(
            ticketId, request, _currentUser.ActiveEntityId, _currentUser.UserId, ct);

        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    [HttpDelete("kb-links/{articleId:guid}")]
    [RequirePermission("servicedesk.write")]
    public async Task<IActionResult> Unlink(Guid ticketId, Guid articleId, CancellationToken ct)
    {
        var result = await _links.UnlinkAsync(ticketId, articleId, ct);
        return result.IsSuccess ? NoContent() : result.Error!.ToProblemResult(HttpContext);
    }
}
