using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Assets.Api.Controllers;

[ApiController]
[Route("api/v1/assets/{assetId:guid}/assignments")]
[Authorize]
public class AssetAssignmentsController : ControllerBase
{
    private readonly IAssetAssignmentService _assignmentService;

    public AssetAssignmentsController(IAssetAssignmentService assignmentService)
        => _assignmentService = assignmentService;

    /// <summary>Assign an asset to a user, department, or location.</summary>
    [HttpPost]
    [RequirePermission("assets.write")]
    public async Task<IActionResult> Assign(
        Guid assetId,
        [FromBody] AssignAssetRequest request,
        [FromServices] Daraban.Platform.Abstractions.ICurrentUser currentUser,
        CancellationToken ct)
    {
        var result = await _assignmentService.AssignAsync(
            assetId, request, currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    /// <summary>Unassign the current assignment from an asset.</summary>
    [HttpDelete("current")]
    [RequirePermission("assets.write")]
    public async Task<IActionResult> Unassign(
        Guid assetId,
        [FromQuery] string? notes,
        [FromServices] Daraban.Platform.Abstractions.ICurrentUser currentUser,
        CancellationToken ct)
    {
        var result = await _assignmentService.UnassignAsync(
            assetId, currentUser.UserId, notes, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return NoContent();
    }

    /// <summary>Get the current (active) assignment for an asset.</summary>
    [HttpGet("current")]
    [RequirePermission("assets.read")]
    public async Task<IActionResult> GetCurrent(Guid assetId, CancellationToken ct)
    {
        var result = await _assignmentService.GetCurrentAsync(assetId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return result.Value is null ? NoContent() : Ok(result.Value);
    }

    /// <summary>Get the full assignment history for an asset.</summary>
    [HttpGet]
    [RequirePermission("assets.read")]
    public async Task<IActionResult> GetHistory(Guid assetId, CancellationToken ct)
    {
        var result = await _assignmentService.GetHistoryAsync(assetId, ct);
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


