using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Modules.ServiceDesk.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.ServiceDesk.Api.Controllers;

[ApiController]
[Route("api/v1/tickets/{ticketId:guid}/tasks")]
[Authorize]
public class TicketTasksController : ControllerBase
{
    private readonly ITicketTaskService _ticketTaskService;
    private readonly ICurrentUser _currentUser;

    public TicketTasksController(ITicketTaskService ticketTaskService, ICurrentUser currentUser)
    {
        _ticketTaskService = ticketTaskService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission("servicedesk.read")]
    public async Task<IActionResult> GetByTicketId(Guid ticketId, CancellationToken ct)
    {
        var result = await _ticketTaskService.GetByTicketIdAsync(ticketId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);
        return Ok(result.Value);
    }

    [HttpPost]
    [RequirePermission("servicedesk.write")]
    public async Task<IActionResult> Create(Guid ticketId, [FromBody] CreateTicketTaskRequest request, CancellationToken ct)
    {
        var result = await _ticketTaskService.CreateAsync(ticketId, request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);
        return CreatedAtAction(nameof(GetByTicketId), new { ticketId }, result.Value);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("servicedesk.write")]
    public async Task<IActionResult> Delete(Guid ticketId, Guid id, CancellationToken ct)
    {
        var result = await _ticketTaskService.DeleteAsync(id, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);
        return NoContent();
    }

}
