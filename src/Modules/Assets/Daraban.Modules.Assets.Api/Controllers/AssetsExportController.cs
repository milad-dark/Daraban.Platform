using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Assets.Api.Controllers;

[ApiController]
[Route("api/v1/assets")]
[Authorize]
public class AssetsExportController(IAssetExportService exportService, ICurrentUser currentUser) : ControllerBase
{

    /// <summary>
    /// Export assets to CSV or Excel with optional filters.
    /// </summary>
    [HttpGet("export")]
    [RequirePermission("assets.read")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(
        [FromQuery] string format = "csv",
        [FromQuery] string? status = null,
        [FromQuery] Guid? assetTypeId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await exportService.ExportAsync(
            currentUser.ActiveEntityId, format, status, assetTypeId, locationId, search, ct);

        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        var (stream, contentType, fileName) = result.Value;
        return File(stream, contentType, fileName);
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
