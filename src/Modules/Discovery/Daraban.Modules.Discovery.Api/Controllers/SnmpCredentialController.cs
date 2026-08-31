using Daraban.Modules.Discovery.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Discovery.Api.Controllers;

/// <summary>
/// API endpoints for managing SNMP credentials (Task 5.1).
/// </summary>
[ApiController]
[Route("api/v1/discovery/credentials")]
[Authorize]
public class SNMPCredentialController(IDiscoveryService discoveryService) : ControllerBase
{

    /// <summary>
    /// Create a new SNMP credential.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "admin:write")]
    public async Task<IActionResult> CreateCredential([FromBody] CreateCredentialRequest request, CancellationToken ct)
    {
        try
        {
            var credential = await discoveryService.CreateCredentialAsync(request, User.Identity?.Name, ct);
            return CreatedAtAction(nameof(GetCredential), new { id = credential.Id }, credential);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific credential by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCredential(Guid id, CancellationToken ct)
    {
        var credential = await discoveryService.GetCredentialByIdAsync(id, ct);
        return credential != null ? Ok(credential) : NotFound();
    }

    /// <summary>
    /// List all SNMP credentials.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCredentials(CancellationToken ct)
    {
        var credentials = await discoveryService.GetAllCredentialsAsync(ct);
        return Ok(credentials);
    }

    /// <summary>
    /// Update an SNMP credential.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "admin:write")]
    public async Task<IActionResult> UpdateCredential(Guid id, [FromBody] UpdateCredentialRequest request, CancellationToken ct)
    {
        try
        {
            var credential = await discoveryService.UpdateCredentialAsync(id, request, ct);
            return Ok(credential);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete an SNMP credential.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "admin:write")]
    public async Task<IActionResult> DeleteCredential(Guid id, CancellationToken ct)
    {
        await discoveryService.DeleteCredentialAsync(id, ct);
        return NoContent();
    }
}
