using Daraban.Modules.Software.Data.Entities;
using Daraban.Modules.Software.Services.Dtos;
using Daraban.Modules.Software.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Software.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SoftwareLicensesController : ControllerBase
{
    private readonly ISoftwareLicenseService _licenseService;
    private readonly ICurrentUser _currentUser;

    public SoftwareLicensesController(ISoftwareLicenseService licenseService, ICurrentUser currentUser)
    {
        _licenseService = licenseService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid entityNodeId,
        [FromQuery] Guid? softwareId = null,
        [FromQuery] LicenseType? type = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _licenseService.GetPagedAsync(entityNodeId, softwareId, type, isActive, page, pageSize, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _licenseService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok(result.Value);
    }

    [HttpGet("software/{softwareId:guid}")]
    public async Task<IActionResult> GetBySoftwareId(Guid softwareId, CancellationToken ct)
    {
        var result = await _licenseService.GetBySoftwareIdAsync(softwareId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSoftwareLicenseRequest request, CancellationToken ct)
    {
        var result = await _licenseService.CreateAsync(request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSoftwareLicenseRequest request, CancellationToken ct)
    {
        var result = await _licenseService.UpdateAsync(id, request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _licenseService.DeleteAsync(id, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return NoContent();
    }

    [HttpGet("{id:guid}/compliance")]
    public async Task<IActionResult> CheckCompliance(Guid id, CancellationToken ct)
    {
        var result = await _licenseService.CheckComplianceAsync(id, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok(result.Value);
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
