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
[Route("api/v1/ticket-templates")]
[Authorize]
public class TicketTemplatesController : ControllerBase
{
    private readonly ITicketTemplateService _ticketTemplateService;
    private readonly ICurrentUser _currentUser;

    public TicketTemplatesController(ITicketTemplateService ticketTemplateService, ICurrentUser currentUser)
    {
        _ticketTemplateService = ticketTemplateService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission("servicedesk.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _ticketTemplateService.GetAllAsync(_currentUser.ActiveEntityId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("servicedesk.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _ticketTemplateService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpPost]
    [RequirePermission("servicedesk.write")]
    public async Task<IActionResult> Create([FromBody] CreateTicketTemplateRequest request, CancellationToken ct)
    {
        var result = await _ticketTemplateService.CreateAsync(request, _currentUser.ActiveEntityId, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("servicedesk.write")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTicketTemplateRequest request, CancellationToken ct)
    {
        var result = await _ticketTemplateService.UpdateAsync(id, request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("servicedesk.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _ticketTemplateService.DeleteAsync(id, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return NoContent();
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
