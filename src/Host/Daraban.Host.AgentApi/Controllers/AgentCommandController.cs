using Daraban.Modules.Identity.Services.Agents;
using Daraban.Platform.Contracts.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Daraban.Host.AgentApi.Controllers;

/// <summary>
/// Agent command lifecycle endpoints (Task 4.4). Agents use these to:
///   - Poll for pending commands:  GET /api/agent/commands/pending
///   - Acknowledge receipt:        POST /api/agent/commands/{commandId}/acknowledge
///   - Report execution result:    POST /api/agent/commands/{commandId}/result
///   - Get result by command ID:   GET /api/agent/commands/{commandId}/result
///
/// These endpoints require the agent scope "commands:submit" (same as the existing
/// AgentCommandsController used for initial command creation).
/// </summary>
[ApiController]
[Route("api/agent/commands")]
[Authorize(Policy = "agent:scope:commands:submit")]
public class AgentCommandController(IAgentCommandService commandService) : ControllerBase
{

    /// <summary>
    /// Poll for pending commands. Agent calls this periodically (e.g. every 30s)
    /// as a fallback when SignalR is disconnected.
    /// GET /api/agent/commands/pending?maxCount=10
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingCommands(
        [FromQuery] int maxCount = 10, CancellationToken ct = default)
    {
        var agentId = GetAgentId();
        if (agentId is null)
            return StatusCode(403, new ProblemDetails { Title = "Agent identity not found in token." });

        var commands = await commandService.GetPendingCommandsAsync(agentId.Value, maxCount, ct);
        return Ok(commands);
    }

    /// <summary>
    /// Agent acknowledges receipt of a command. Server marks it as Received.
    /// POST /api/agent/commands/{commandId}/acknowledge
    /// </summary>
    [HttpPost("{commandId:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeCommand(Guid commandId, CancellationToken ct)
    {
        var agentId = GetAgentId();
        if (agentId is null)
            return StatusCode(403, new ProblemDetails { Title = "Agent identity not found in token." });

        var ok = await commandService.AcknowledgeCommandAsync(agentId.Value, commandId, ct);
        if (!ok)
            return NotFound(new ProblemDetails
            {
                Title = "Command not found or not addressed to this agent.",
                Status = 404,
            });

        return Ok(new { commandId, status = "acknowledged" });
    }

    /// <summary>
    /// Agent reports the result of executing a command.
    /// POST /api/agent/commands/{commandId}/result
    /// </summary>
    [HttpPost("{commandId:guid}/result")]
    public async Task<IActionResult> ReportResult(
        Guid commandId, [FromBody] CommandResultRequest request, CancellationToken ct)
    {
        var agentId = GetAgentId();
        if (agentId is null)
            return StatusCode(403, new ProblemDetails { Title = "Agent identity not found in token." });

        try
        {
            var response = await commandService.ReportResultAsync(
                agentId.Value, commandId, request, ct);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = ex.Message,
                Status = 404,
            });
        }
    }

    /// <summary>
    /// Agent retrieves the result of a previously executed command.
    /// GET /api/agent/commands/{commandId}/result
    /// </summary>
    [HttpGet("{commandId:guid}/result")]
    public async Task<IActionResult> GetResult(Guid commandId, CancellationToken ct)
    {
        var agentId = GetAgentId();
        if (agentId is null)
            return StatusCode(403, new ProblemDetails { Title = "Agent identity not found in token." });

        var command = await commandService.GetCommandAsync(commandId, ct);
        if (command is null || command.AgentId != agentId.Value)
            return NotFound(new ProblemDetails
            {
                Title = "Command not found.",
                Status = 404,
            });

        return Ok(command);
    }

    private Guid? GetAgentId()
    {
        var isAgent = User.FindFirst("is_agent")?.Value;
        if (isAgent != "true")
            return null;
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return sub is not null ? Guid.Parse(sub) : null;
    }
}
