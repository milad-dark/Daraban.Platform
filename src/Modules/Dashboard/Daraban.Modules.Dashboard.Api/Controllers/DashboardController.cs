using Daraban.Modules.Dashboard.Services.Dtos;
using Daraban.Modules.Dashboard.Services.Interfaces;
using Daraban.Modules.Dashboard.Services.Widgets;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Dashboard.Api.Controllers;

/// <summary>
/// Dashboard endpoints (Task 7.1). The controller is a thin adapter: parse the route, call the
/// service, map Result to HTTP. Every endpoint requires authentication via the host's fallback
/// authorization policy.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public sealed class DashboardController(
    IDashboardService dashboardService,
    ICurrentUser currentUser) : ControllerBase
{
    // ---- Widget catalog ------------------------------------------------------

    /// <summary>All available widget types with display metadata.</summary>
    /// <remarks>GET /api/v1/dashboard/widgets</remarks>
    [HttpGet("widgets")]
    public IActionResult GetWidgets()
    {
        var result = dashboardService.GetWidgetCatalog();
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    // ---- Layout --------------------------------------------------------------

    /// <summary>The calling user's saved dashboard layout (empty when none saved yet).</summary>
    /// <remarks>GET /api/v1/dashboard/layout</remarks>
    [HttpGet("layout")]
    public async Task<IActionResult> GetLayout(CancellationToken ct)
    {
        var result = await dashboardService.GetLayoutAsync(currentUser.UserId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    /// <summary>Saves the calling user's dashboard layout. Strictly per-user: the userId always
    /// comes from the JWT, never from the request body (OWASP A01 -- no IDOR surface).</summary>
    /// <remarks>PUT /api/v1/dashboard/layout</remarks>
    [HttpPut("layout")]
    public async Task<IActionResult> SaveLayout([FromBody] SaveLayoutRequest request, CancellationToken ct)
    {
        var result = await dashboardService.SaveLayoutAsync(currentUser.UserId, request, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    // ---- Widget data ----------------------------------------------------------

    /// <summary>
    /// Data payload for one widget. The widgetType route value is parsed against the closed
    /// WidgetCatalog -- unknown values yield 404, so no arbitrary input reaches a provider.
    /// </summary>
    /// <remarks>GET /api/v1/dashboard/data/{widgetType}</remarks>
    [HttpGet("data/{widgetType}")]
    public async Task<IActionResult> GetWidgetData(string widgetType, CancellationToken ct)
    {
        if (!WidgetCatalog.TryParse(widgetType, out var type))
        {
            return Result.Failure<WidgetDataDto>(
                new Error("DASHBOARD.WIDGET_NOT_FOUND", $"Unknown widget type '{widgetType}'.", ErrorType.NotFound))
                .Error!.ToProblemResult(HttpContext);
        }

        var result = await dashboardService.GetWidgetDataAsync(type, currentUser.ActiveEntityId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }
}
