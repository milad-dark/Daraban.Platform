using Daraban.Modules.Inventory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Daraban.Host.AgentApi.Controllers;

/// <summary>
/// Receives inventory submissions from agents. Routes match the exact paths the
/// DarabanClient sends to (see DarabanClient.SendEnvelopeAsync and individual
/// PostInventoryAsync/PostDiscoveryAsync/PostNetInventoryAsync methods).
///
/// The Agent formats are:
///   POST /api/agent/inventory    — local/remote inventory (DeviceInventory)
///   POST /api/agent/discovery    — network discovery (DiscoveredHost[])
///   POST /api/agent/netinventory — SNMP network inventory (NetworkDeviceInventory[])
///   POST /api/agent/esx          — ESXi inventory (EsxHostInfo)
///   POST /api/agent/wakeonlan    — WoL results (WakeOnLanResult[])
///   POST /api/agent/deploy/result— deploy job result (DeployJobResult)
///   GET  /api/agent/prolog       — device prolog check
///   GET  /api/agent/deploy/jobs  — pending deploy jobs
///   GET  /api/agent/collect/jobs — pending collect jobs
///   POST /api/agent/collect/results — collect task results
/// </summary>
[ApiController]
[Route("api/agent")]
[Authorize(Policy = "agent:scope:inventory:write")]
public class AgentInventoryController(
    IInventoryService inventoryService,
    ILogger<AgentInventoryController> logger) : ControllerBase
{

    /// <summary>
    /// Receive raw inventory from an agent. Matches DarabanClient.PostInventoryAsync().
    /// The Agent sends a JSON envelope: { deviceId, itemtype, action, timestampUtc, content }.
    /// </summary>
    [HttpPost("inventory")]
    [Consumes("application/json")]
    public Task<IActionResult> ReceiveInventory([FromBody] AgentEnvelope envelope, CancellationToken ct)
        => SubmitEnvelopeAsync(envelope, "Inventory", ct);

    [HttpPost("discovery")]
    [Consumes("application/json")]
    public Task<IActionResult> ReceiveDiscovery([FromBody] AgentEnvelope envelope, CancellationToken ct)
        => SubmitEnvelopeAsync(envelope, "Discovery", ct);

    [HttpPost("netinventory")]
    [Consumes("application/json")]
    public Task<IActionResult> ReceiveNetInventory([FromBody] AgentEnvelope envelope, CancellationToken ct)
        => SubmitEnvelopeAsync(envelope, "NetInventory", ct);

    [HttpPost("esx")]
    [Consumes("application/json")]
    public Task<IActionResult> ReceiveEsxInventory([FromBody] AgentEnvelope envelope, CancellationToken ct)
        => SubmitEnvelopeAsync(envelope, "EsxInventory", ct);

    [HttpPost("wakeonlan")]
    [Consumes("application/json")]
    public Task<IActionResult> ReceiveWakeOnLan([FromBody] AgentEnvelope envelope, CancellationToken ct)
        => SubmitEnvelopeAsync(envelope, "WakeOnLan", ct);

    [HttpPost("deploy/result")]
    [Consumes("application/json")]
    public Task<IActionResult> ReceiveDeployResult([FromBody] AgentEnvelope envelope, CancellationToken ct)
        => SubmitEnvelopeAsync(envelope, "DeployResult", ct);

    // ── GET endpoints (prolog, deploy/jobs) ─────────────────────────────────

    /// <summary>
    /// Device prolog check. Agent sends this before submitting inventory to check
    /// if the device is already known. Matches DarabanClient.PrologAsync().
    /// Open (no auth) because agents call this before authenticating.
    /// </summary>
    [HttpGet("prolog")]
    [AllowAnonymous]
    public IActionResult Prolog([FromQuery] string deviceId)
    {
        // TODO: Return device status from the Asset domain (Task 5.x)
        return Ok(new { deviceId, known = false, message = "Prolog endpoint placeholder." });
    }

    /// <summary>
    /// Get pending deploy jobs for this agent. Matches DarabanClient.GetPendingDeployJobsAsync().
    /// </summary>
    [HttpGet("deploy/jobs")]
    public IActionResult GetDeployJobs([FromQuery] string deviceId)
    {
        // TODO: Implement with Deploy module (Task 5.x)
        return Ok(Array.Empty<object>());
    }

    // ── Shared submission logic ─────────────────────────────────────────────

    /// <summary>
    /// All POST endpoints share the same logic: extract agent ID, submit envelope,
    /// return 202 Accepted or 400 BadRequest.
    /// </summary>
    private async Task<IActionResult> SubmitEnvelopeAsync(
        AgentEnvelope envelope, string logLabel, CancellationToken ct)
    {
        var agentId = GetAgentId();
        if (agentId is null)
            return Forbid();

        logger.LogInformation(
            "{Label} received from agent {AgentId} for device {DeviceId}, action={Action}",
            logLabel, agentId, envelope.DeviceId, envelope.Action);

        // EntityId is null — agent tokens never contain entity_id (pre-existing gap).
        var result = await inventoryService.SubmitAsync(
            envelope, agentId.Value, entityId: null,
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

        if (!result.IsSuccess)
            return Problem(result.Error!.Message, statusCode: StatusCodes.Status400BadRequest);

        return StatusCode(StatusCodes.Status202Accepted, result.Value);
    }

    private Guid? GetAgentId()
    {
        var isAgent = User.FindFirst("is_agent")?.Value;
        if (isAgent != "true")
            return null;
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return sub is not null ? Guid.Parse(sub) : null;
    }
}
