using Daraban.Modules.Discovery.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Daraban.Modules.Discovery.Api.Controllers;

/// <summary>
/// API endpoints for managing discovery ranges (Task 5.1).
/// </summary>
[ApiController]
[Route("api/v1/discovery/ranges")]
[Authorize]
public class DiscoveryRangeController(IDiscoveryService discoveryService) : ControllerBase
{

    /// <summary>
    /// Create a new discovery range.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "admin:write")]
    public async Task<IActionResult> CreateRange([FromBody] CreateRangeRequest request, CancellationToken ct)
    {
        try
        {
            var range = await discoveryService.CreateRangeAsync(request, User.Identity?.Name, ct);
            return CreatedAtAction(nameof(GetRange), new { id = range.Id }, range);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific discovery range by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRange(Guid id, CancellationToken ct)
    {
        var range = await discoveryService.GetRangeByIdAsync(id, ct);
        return range != null ? Ok(range) : NotFound();
    }

    /// <summary>
    /// List all discovery ranges.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRanges(CancellationToken ct)
    {
        var ranges = await discoveryService.GetAllRangesAsync(ct);
        return Ok(ranges);
    }

    /// <summary>
    /// Update a discovery range.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "admin:write")]
    public async Task<IActionResult> UpdateRange(Guid id, [FromBody] UpdateRangeRequest request, CancellationToken ct)
    {
        try
        {
            var range = await discoveryService.UpdateRangeAsync(id, request, ct);
            return Ok(range);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a discovery range.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "admin:write")]
    public async Task<IActionResult> DeleteRange(Guid id, CancellationToken ct)
    {
        await discoveryService.DeleteRangeAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Start a scan on a discovery range.
    /// </summary>
    [HttpPost("{id:guid}/scan")]
    [Authorize(Policy = "admin:write")]
    [EnableRateLimiting("discovery-scan")]
    public async Task<IActionResult> StartScan(Guid id, [FromBody] StartScanRequest? request, CancellationToken ct)
    {
        try
        {
            var scanRequest = request != null
                ? request with { RangeId = id }
                : new StartScanRequest(id);

            var scan = await discoveryService.StartScanAsync(scanRequest, User.Identity?.Name, ct);
            return AcceptedAtAction(nameof(DiscoveryScanController.GetScan), "DiscoveryScan", new { id = scan.Id }, scan);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
