using Daraban.Modules.Software.Services.Dtos;
using Daraban.Modules.Software.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Software.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SoftwareInstallationsController : ControllerBase
{
    private readonly ISoftwareInstallationService _installationService;
    private readonly ICurrentUser _currentUser;

    public SoftwareInstallationsController(ISoftwareInstallationService installationService, ICurrentUser currentUser)
    {
        _installationService = installationService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid entityNodeId,
        [FromQuery] Guid? softwareId = null,
        [FromQuery] Guid? licenseId = null,
        [FromQuery] Guid? assetId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _installationService.GetPagedAsync(entityNodeId, softwareId, licenseId, assetId, isActive, page, pageSize, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _installationService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok(result.Value);
    }

    [HttpGet("asset/{assetId:guid}")]
    public async Task<IActionResult> GetByAssetId(Guid assetId, CancellationToken ct)
    {
        var result = await _installationService.GetByAssetIdAsync(assetId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok(result.Value);
    }

    [HttpGet("software/{softwareId:guid}")]
    public async Task<IActionResult> GetBySoftwareId(Guid softwareId, CancellationToken ct)
    {
        var result = await _installationService.GetBySoftwareIdAsync(softwareId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSoftwareInstallationRequest request, CancellationToken ct)
    {
        var result = await _installationService.CreateAsync(request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPost("{id:guid}/uninstall")]
    public async Task<IActionResult> Uninstall(Guid id, CancellationToken ct)
    {
        var result = await _installationService.UninstallAsync(id, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok();
    }

    [HttpGet("asset/{assetId:guid}/summary")]
    public async Task<IActionResult> GetAssetSummary(Guid assetId, CancellationToken ct)
    {
        var result = await _installationService.GetAssetSummaryAsync(assetId, ct);
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
