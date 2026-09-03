using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Modules.ServiceDesk.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.ServiceDesk.Api.Controllers;

[ApiController]
[Route("api/v1/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;
    private readonly ICurrentUser _currentUser;

    public TicketsController(ITicketService ticketService, ICurrentUser currentUser)
    {
        _ticketService = ticketService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission("servicedesk.read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] TicketType? type,
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketPriority? priority,
        [FromQuery] Guid? assignedUserId,
        [FromQuery] Guid? assignedGroupId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _ticketService.GetPagedAsync(
            _currentUser.ActiveEntityId, type, status, priority, assignedUserId, assignedGroupId, search, page, pageSize, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("servicedesk.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _ticketService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpPost]
    [RequirePermission("servicedesk.write")]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var result = await _ticketService.CreateAsync(request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("servicedesk.write")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTicketRequest request, CancellationToken ct)
    {
        var result = await _ticketService.UpdateAsync(id, request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("servicedesk.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _ticketService.DeleteAsync(id, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [RequirePermission("servicedesk.write")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeStatusRequest request, CancellationToken ct)
    {
        var result = await _ticketService.ChangeStatusAsync(id, request.Status, _currentUser.UserId, request.Reason, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/assign")]
    [RequirePermission("servicedesk.write")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTicketRequest request, CancellationToken ct)
    {
        var result = await _ticketService.AssignAsync(id, request.AssignedUserId, request.AssignedGroupId, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/escalate")]
    [RequirePermission("servicedesk.write")]
    public async Task<IActionResult> Escalate(Guid id, CancellationToken ct)
    {
        var result = await _ticketService.EscalateAsync(id, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/solve")]
    [RequirePermission("servicedesk.write")]
    public async Task<IActionResult> Solve(Guid id, [FromBody] SolveTicketRequest request, CancellationToken ct)
    {
        var result = await _ticketService.SolveAsync(id, _currentUser.UserId, request.Solution, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpPut("{id:guid}/close")]
    [RequirePermission("servicedesk.write")]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var result = await _ticketService.CloseAsync(id, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpGet("count/open")]
    [RequirePermission("servicedesk.read")]
    public async Task<IActionResult> GetOpenCount(CancellationToken ct)
    {
        var result = await _ticketService.GetOpenCountAsync(_currentUser.ActiveEntityId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(new { count = result.Value });
    }

    [HttpGet("count/overdue")]
    [RequirePermission("servicedesk.read")]
    public async Task<IActionResult> GetOverdueCount(CancellationToken ct)
    {
        var result = await _ticketService.GetOverdueCountAsync(_currentUser.ActiveEntityId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(new { count = result.Value });
    }

    private ObjectResult ProblemFrom(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest,
        };
        return new ObjectResult(new ProblemDetails
        {
            Title = error.Message,
            Status = status,
            Extensions = { ["errorCode"] = error.Code },
        })
        { StatusCode = status };
    }
}

// ---- Request DTOs for controller actions ----
public record ChangeStatusRequest(TicketStatus Status, string? Reason);
public record AssignTicketRequest(Guid? AssignedUserId, Guid? AssignedGroupId);
public record SolveTicketRequest(string? Solution);
