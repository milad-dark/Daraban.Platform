using Daraban.Modules.Reporting.Services.Dtos;
using Daraban.Modules.Reporting.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Reporting.Api.Controllers;

/// <summary>
/// Reporting endpoints (Task 7.2). Thin adapter: parse the route, call the service, map
/// Result to HTTP. Every endpoint requires authentication via the host's fallback policy;
/// entity scoping always comes from the JWT, never from the request (OWASP A01 -- no IDOR
/// surface), so all service calls receive currentUser.ActiveEntityId.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/reports")]
public sealed class ReportsController(
    IReportingService reportingService,
    ICurrentUser currentUser) : ControllerBase
{
    // ---- Catalog ------------------------------------------------------------------

    /// <summary>All reportable datasets with their columns and supported filters.</summary>
    /// <remarks>GET /api/v1/reports/definitions</remarks>
    [HttpGet("definitions")]
    public IActionResult GetCatalog()
    {
        var result = reportingService.GetCatalog();
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    // ---- Definitions ----------------------------------------------------------------

    /// <summary>The caller's entity's saved report definitions.</summary>
    /// <remarks>GET /api/v1/reports/my</remarks>
    [HttpGet("my")]
    public async Task<IActionResult> ListDefinitions(CancellationToken ct)
    {
        var result = await reportingService.ListDefinitionsAsync(currentUser.ActiveEntityId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    /// <summary>Creates a report definition. Name/module/format/columns validate against the
    /// closed ReportCatalog; schedule (optional) must be a standard 5-field cron expression.</summary>
    /// <remarks>POST /api/v1/reports/definitions</remarks>
    [HttpPost("definitions")]
    [RequirePermission("reports.manage")]
    public async Task<IActionResult> CreateDefinition([FromBody] CreateReportDefinitionRequest request, CancellationToken ct)
    {
        var result = await reportingService.CreateDefinitionAsync(
            currentUser.ActiveEntityId, currentUser.UserId, request, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    // ---- Generation -----------------------------------------------------------------

    /// <summary>Queues a generation run for one definition. Returns immediately with the
    /// Pending SavedReport; the worker renders asynchronously. Poll GET runs / download.</summary>
    /// <remarks>POST /api/v1/reports/{id}/generate</remarks>
    [HttpPost("{id:guid}/generate")]
    public async Task<IActionResult> Generate(Guid id, CancellationToken ct)
    {
        var result = await reportingService.GenerateAsync(
            id, currentUser.ActiveEntityId, currentUser.UserId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Accepted(result.Value);
    }

    /// <summary>Generation history (latest 20 runs) for one definition.</summary>
    /// <remarks>GET /api/v1/reports/{id}/runs</remarks>
    [HttpGet("{id:guid}/runs")]
    public async Task<IActionResult> ListRuns(Guid id, CancellationToken ct)
    {
        var result = await reportingService.ListRunsAsync(id, currentUser.ActiveEntityId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    /// <summary>Streams the completed artifact for one SavedReport. Only Completed runs and
    /// artifacts still present in the file store resolve; anything else is a ProblemDetails
    /// error, never a partial file.</summary>
    /// <remarks>GET /api/v1/reports/{id}/download</remarks>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var result = await reportingService.DownloadAsync(id, currentUser.ActiveEntityId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        var (stream, contentType, fileName) = result.Value;
        return File(stream, contentType, fileName);
    }
}
