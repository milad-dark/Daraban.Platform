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
    public async Task<IActionResult> GetAllRules(CancellationToken ct)
    {
        var rules = await _importRuleService.GetAllRulesAsync(ct);
        return Ok(rules);
    }

    /// <summary>
    /// Get all active import rules.
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveRules(CancellationToken ct)
    {
        var rules = await _importRuleService.GetActiveRulesAsync(ct);
        return Ok(rules);
    }

    /// <summary>
    /// Get an import rule by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRuleById(Guid id, CancellationToken ct)
    {
        var rule = await _importRuleService.GetRuleByIdAsync(id, ct);
        return rule != null ? Ok(rule) : NotFound();
    }

    /// <summary>
    /// Create a new import rule.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "admin:write")]
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
    [Authorize(Policy = "admin:write")]
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
    [Authorize(Policy = "admin:write")]
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
    public async Task<IActionResult> EvaluateDevice([FromBody] DeviceResponse device, CancellationToken ct)
    {
        var result = await _importRuleService.EvaluateDeviceAsync(device, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get available fields for import rule criteria.
    /// </summary>
    [HttpGet("fields")]
    public IActionResult GetAvailableFields()
    {
        return Ok(ImportRuleFields.All);
    }

    /// <summary>
    /// Get available operators for import rule criteria.
    /// </summary>
    [HttpGet("operators")]
    public IActionResult GetAvailableOperators()
    {
        return Ok(ImportRuleOperators.All);
    }

    /// <summary>
    /// Get available action types for import rule actions.
    /// </summary>
    [HttpGet("action-types")]
    public IActionResult GetAvailableActionTypes()
    {
        return Ok(ImportRuleActionTypes.All);
    }
}
