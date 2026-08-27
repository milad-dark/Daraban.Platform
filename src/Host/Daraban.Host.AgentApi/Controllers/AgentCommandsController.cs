using System.Security.Claims;
using Daraban.Modules.Identity.Services.Agents;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Contracts.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Host.AgentApi.Controllers;

/// <summary>
/// Agent command submission — agents POST async commands that are dispatched via RabbitMQ
/// to the appropriate module's worker (Task 4.1 SS2.4).
/// </summary>
[ApiController]
[Route("api/v1/agents/commands")]
[Authorize(Policy = "agent:scope:commands:submit")]
public class AgentCommandsController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly IEventPublisher _eventPublisher;

    public AgentCommandsController(IAgentService agentService, IEventPublisher eventPublisher)
    {
        _agentService = agentService;
        _eventPublisher = eventPublisher;
    }

    /// <summary>
    /// Submit an async command for processing. Returns immediately with a command ID.
    /// The actual work is done by a background worker via RabbitMQ.
    /// POST /api/v1/agents/commands
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SubmitCommand(
        [FromBody] AgentCommandRequest request, CancellationToken ct)
    {
        var agentId = GetAgentId();
        if (agentId is null)
            return StatusCode(403, new ProblemDetails { Title = "Agent identity not found in token." });

        // Validate scope
        var requiredScope = request.TargetModule is not null
            ? $"{request.TargetModule}:write"
            : "commands:submit";

        var hasScope = await _agentService.ValidateScopesAsync(agentId.Value, [requiredScope], ct);
        if (!hasScope)
            return StatusCode(403, new ProblemDetails
            {
                Title = "Insufficient scope.",
                Extensions = { ["errorCode"] = "AGENTS.SCOPE_DENIED" },
                Status = 403,
            });

        var commandId = Guid.NewGuid();
        var queuedAt = DateTimeOffset.UtcNow;

        // Publish to RabbitMQ for async processing by the target module's worker
        await _eventPublisher.PublishAsync(new AgentCommandPublishedEvent(
            commandId, agentId.Value, request.CommandType,
            request.TargetModule, request.Payload,
            request.TimeoutSeconds, queuedAt), ct);

        // Audit
        await _agentService.LogActionAsync(
            agentId.Value, null, "command.submitted",
            $"type={request.CommandType}, target={request.TargetModule}",
            202, HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            null, true, null, HttpContext.TraceIdentifier,
            null, $"{{\"commandId\":\"{commandId}\"}}", ct);

        return Accepted(new AgentCommandResponse(commandId, "queued", queuedAt));
    }

    private Guid? GetAgentId()
    {
        var isAgent = User.FindFirst("is_agent")?.Value;
        if (isAgent != "true") return null;
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return sub is not null ? Guid.Parse(sub) : null;
    }
}
