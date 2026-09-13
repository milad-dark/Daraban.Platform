using Daraban.Platform.Hosting.Authorization;
using Daraban.Modules.Discovery.Data.Entities;
using Daraban.Modules.Discovery.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Discovery.Api.Controllers;

/// <summary>
/// API controller for managing import rules (GLPI-style).
/// Provides CRUD operations and rule evaluation.
/// </summary>
[ApiController]
[Route("api/v1/discovery/import-rules")]
[Authorize]
public class ImportRuleController : ControllerBase
{
    private readonly IImportRuleService _importRuleService;

    public ImportRuleController(IImportRuleService importRuleService)
    {
        _importRuleService = importRuleService;
    }

    /// <summary>
    /// Get all import rules.
    /// </summary>
    [HttpGet]
    [RequirePermission("discovery.read")]
    public async Task<IActionResult> GetAllRules(CancellationToken ct)
    {
        var rules = await _importRuleService.GetAllRulesAsync(ct);
        return Ok(rules);
    }

    /// <summary>
    /// Get all active import rules.
    /// </summary>
    [HttpGet("active")]
    [RequirePermission("discovery.read")]
    public async Task<IActionResult> GetActiveRules(CancellationToken ct)
    {
        var rules = await _importRuleService.GetActiveRulesAsync(ct);
        return Ok(rules);
    }

    /// <summary>
    /// Get an import rule by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequirePermission("discovery.read")]
    public async Task<IActionResult> GetRuleById(Guid id, CancellationToken ct)
    {
        var rule = await _importRuleService.GetRuleByIdAsync(id, ct);
        return rule != null ? Ok(rule) : NotFound();
    }

    /// <summary>
    /// Create a new import rule.
    /// </summary>
    [HttpPost]
    [RequirePermission("discovery.write")]
    public async Task<IActionResult> CreateRule([FromBody] CreateImportRuleRequest request, CancellationToken ct)
    {
        try
        {
            var rule = await _importRuleService.CreateRuleAsync(request, User.Identity?.Name, ct);
            return CreatedAtAction(nameof(GetRuleById), new { id = rule.Id }, rule);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an import rule.
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequirePermission("discovery.write")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateImportRuleRequest request, CancellationToken ct)
    {
        try
        {
            var rule = await _importRuleService.UpdateRuleAsync(id, request, ct);
            return Ok(rule);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete an import rule.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission("discovery.delete")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct)
    {
        try
        {
            await _importRuleService.DeleteRuleAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Evaluate a device against import rules.
    /// </summary>
    [HttpPost("evaluate")]
    [RequirePermission("discovery.write")]
    public async Task<IActionResult> EvaluateDevice([FromBody] DeviceResponse device, CancellationToken ct)
    {
        var result = await _importRuleService.EvaluateDeviceAsync(device, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get available fields for import rule criteria.
    /// </summary>
    [HttpGet("fields")]
    [RequirePermission("discovery.read")]
    public IActionResult GetAvailableFields()
    {
        return Ok(ImportRuleFields.All);
    }

    /// <summary>
    /// Get available operators for import rule criteria.
    /// </summary>
    [HttpGet("operators")]
    [RequirePermission("discovery.read")]
    public IActionResult GetAvailableOperators()
    {
        return Ok(ImportRuleOperators.All);
    }

    /// <summary>
    /// Get available action types for import rule actions.
    /// </summary>
    [HttpGet("action-types")]
    [RequirePermission("discovery.read")]
    public IActionResult GetAvailableActionTypes()
    {
        return Ok(ImportRuleActionTypes.All);
    }
}
