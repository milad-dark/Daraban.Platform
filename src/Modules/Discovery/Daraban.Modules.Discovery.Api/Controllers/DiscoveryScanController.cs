using Daraban.Modules.Discovery.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Discovery.Api.Controllers;

/// <summary>
/// API endpoints for managing discovery scans (Task 5.1).
/// </summary>
[ApiController]
[Route("api/v1/discovery/scans")]
[Authorize]
public class DiscoveryScanController(IDiscoveryService discoveryService) : ControllerBase
{

    /// <summary>
    /// Get a specific scan by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetScan(Guid id, CancellationToken ct)
    {
        var scan = await discoveryService.GetScanByIdAsync(id, ct);
        return scan != null ? Ok(scan) : NotFound();
    }

    /// <summary>
    /// List scans for a specific range (paginated).
    /// </summary>
    [HttpGet("range/{rangeId:guid}")]
    public async Task<IActionResult> GetScansByRange(Guid rangeId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var scans = await discoveryService.GetScansByRangeIdAsync(rangeId, page, pageSize, ct);
        return Ok(scans);
    }

    /// <summary>
    /// Get recent scans across all ranges.
    /// </summary>
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentScans([FromQuery] int count = 10, CancellationToken ct = default)
    {
        var scans = await discoveryService.GetRecentScansAsync(count, ct);
        return Ok(scans);
    }

    /// <summary>
    /// Get devices discovered by a specific scan.
    /// </summary>
    [HttpGet("{id:guid}/devices")]
    public async Task<IActionResult> GetDevicesByScan(Guid id, CancellationToken ct)
    {
        var devices = await discoveryService.GetDevicesByScanIdAsync(id, ct);
        return Ok(devices);
    }
}
