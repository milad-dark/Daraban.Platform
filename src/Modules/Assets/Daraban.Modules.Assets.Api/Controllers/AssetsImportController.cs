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
[Route("api/v1/assets")]
[Authorize]
public class AssetsImportController(IAssetImportService importService, ICurrentUser currentUser) : ControllerBase
{

    /// <summary>
    /// Import assets from a CSV or Excel file.
    /// </summary>
    /// <param name="file">CSV or XLSX file</param>
    /// <param name="dryRun">If true, validates without writing to DB</param>
    /// <param name="ct">Cancellation token</param>
    [HttpPost("import")]
    [RequirePermission("assets.write")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import(
        IFormFile file,
        [FromQuery] bool dryRun = false,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return Problem("No file uploaded.", statusCode: StatusCodes.Status400BadRequest);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".csv" or ".xlsx"))
            return Problem("Only CSV and XLSX files are supported.", statusCode: StatusCodes.Status400BadRequest);

        await using var stream = file.OpenReadStream();
        var result = await importService.ImportAsync(
            stream, file.FileName, currentUser.ActiveEntityId, currentUser.UserId, dryRun, ct);

        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return Ok(result.Value);
    }

    /// <summary>
    /// Download a CSV template for asset import.
    /// </summary>
    [HttpGet("import/template")]
    [RequirePermission("assets.read")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public IActionResult GetTemplate()
    {
        var stream = importService.GetTemplate();
        return File(stream, "text/csv", "asset-import-template.csv");
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
