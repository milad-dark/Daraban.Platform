using Daraban.Modules.Software.Data.Entities;
using Daraban.Modules.Software.Services.Dtos;
using Daraban.Modules.Software.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Software.Api.Controllers;

[ApiController]
[Route("api/v1/softwares")]
[Authorize]
public class SoftwaresController : ControllerBase
{
    private readonly ISoftwareService _softwareService;
    private readonly ICurrentUser _currentUser;

    public SoftwaresController(ISoftwareService softwareService, ICurrentUser currentUser)
    {
        _softwareService = softwareService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission("software.read")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? search = null,
        [FromQuery] SoftwareCategory? category = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // The tenant comes from the caller's validated JWT, never from the query string -- the
        // previous signature accepted entityNodeId from the client, so passing another entity's
        // id read that tenant's catalog (horizontal privilege escalation).
        var result = await _softwareService.GetPagedAsync(
            _currentUser.ActiveEntityId, search, category, isActive, page, pageSize, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("software.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _softwareService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok(result.Value);
    }

    [HttpPost]
    [RequirePermission("software.write")]
    public async Task<IActionResult> Create([FromBody] CreateSoftwareRequest request, CancellationToken ct)
    {
        // Stamp the tenant server-side: a client-supplied EntityNodeId would let any caller file
        // catalog entries into another tenant.
        var result = await _softwareService.CreateAsync(
            request with { EntityNodeId = _currentUser.ActiveEntityId }, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("software.write")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSoftwareRequest request, CancellationToken ct)
    {
        var result = await _softwareService.UpdateAsync(id, request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("software.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _softwareService.DeleteAsync(id, _currentUser.UserId, ct);
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
