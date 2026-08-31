using Daraban.Modules.Discovery.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Discovery.Api.Controllers;

/// <summary>
/// API endpoints for managing discovery rules (Task 5.1).
/// </summary>
[ApiController]
[Route("api/v1/discovery/rules")]
[Authorize]
public class DiscoveryRuleController(IDiscoveryService discoveryService) : ControllerBase
{

    /// <summary>
    /// Create a new discovery rule.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "admin:write")]
    public async Task<IActionResult> CreateRule([FromBody] CreateRuleRequest request, CancellationToken ct)
    {
        try
        {
            var rule = await discoveryService.CreateRuleAsync(request, User.Identity?.Name, ct);
            return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, rule);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific rule by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRule(Guid id, CancellationToken ct)
    {
        var rule = await discoveryService.GetRuleByIdAsync(id, ct);
        return rule != null ? Ok(rule) : NotFound();
    }

    /// <summary>
    /// List all discovery rules.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRules(CancellationToken ct)
    {
        var rules = await discoveryService.GetAllRulesAsync(ct);
        return Ok(rules);
    }

    /// <summary>
    /// Update a discovery rule.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "admin:write")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateRuleRequest request, CancellationToken ct)
    {
        try
        {
            var rule = await discoveryService.UpdateRuleAsync(id, request, ct);
            return Ok(rule);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a discovery rule.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "admin:write")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct)
    {
        await discoveryService.DeleteRuleAsync(id, ct);
        return NoContent();
    }
}
