using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Assets.Api.Controllers;

[ApiController]
[Route("api/v1/assets/{assetId:guid}/lifecycle")]
[Authorize]
public class AssetLifecycleController : ControllerBase
{
    private readonly IAssetLifecycleService _lifecycleService;
    private readonly ICurrentUser _currentUser;

    public AssetLifecycleController(IAssetLifecycleService lifecycleService, ICurrentUser currentUser)
    {
        _lifecycleService = lifecycleService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Transition an asset to a new lifecycle status.
    /// Allowed transitions: Archive, Retire, Dispose, Transfer, Restore.
    /// </summary>
    [HttpPost("transition")]
    [RequirePermission("assets.write")]
    public async Task<IActionResult> Transition(
        Guid assetId,
        [FromBody] LifecycleTransitionRequest request,
        CancellationToken ct)
    {
        var result = await _lifecycleService.TransitionAsync(
            assetId, request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    /// <summary>Get the full lifecycle history for an asset.</summary>
    [HttpGet("history")]
    [RequirePermission("assets.read")]
    public async Task<IActionResult> GetHistory(Guid assetId, CancellationToken ct)
    {
        var result = await _lifecycleService.GetHistoryAsync(assetId, ct);
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
