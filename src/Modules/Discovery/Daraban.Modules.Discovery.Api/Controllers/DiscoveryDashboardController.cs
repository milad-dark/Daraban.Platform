using Daraban.Modules.Discovery.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Discovery.Api.Controllers;

/// <summary>
/// API endpoints for Discovery dashboard (Task 5.1).
/// </summary>
[ApiController]
[Route("api/v1/discovery")]
[Authorize]
public class DiscoveryDashboardController(IDiscoveryService discoveryService) : ControllerBase
{

    /// <summary>
    /// Get discovery dashboard summary.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var dashboard = await discoveryService.GetDashboardAsync(ct);
        return Ok(dashboard);
    }

    /// <summary>
    /// Get all discovered devices for a range.
    /// </summary>
    [HttpGet("ranges/{rangeId:guid}/devices")]
    public async Task<IActionResult> GetDevicesByRange(Guid rangeId, CancellationToken ct)
    {
        var devices = await discoveryService.GetDevicesByRangeIdAsync(rangeId, ct);
        return Ok(devices);
    }

    /// <summary>
    /// Get recent discovered devices.
    /// </summary>
    [HttpGet("devices/recent")]
    public async Task<IActionResult> GetRecentDevices([FromQuery] int count = 10, CancellationToken ct = default)
    {
        var devices = await discoveryService.GetRecentDevicesAsync(count, ct);
        return Ok(devices);
    }
}
