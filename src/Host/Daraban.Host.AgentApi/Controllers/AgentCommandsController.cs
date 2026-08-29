using Daraban.Modules.Identity.Services.Agents;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Contracts.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Daraban.Host.AgentApi.Controllers;

/// <summary>
/// Agent command submission — agents POST async commands that are persisted to the DB
/// and dispatched via RabbitMQ to connected agents (Task 4.4).
/// </summary>
[ApiController]
[Route("api/v1/agents/commands")]
[Authorize(Policy = "agent:scope:commands:submit")]
public class AgentCommandsController(
    IAgentService agentService,
    IAgentCommandService commandService,
    IEventPublisher eventPublisher) : ControllerBase
{

    /// <summary>
    /// Submit an async command for processing. Creates a DB record, publishes to
    /// RabbitMQ for SignalR dispatch, and returns immediately with a command ID.
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

        var hasScope = await agentService.ValidateScopesAsync(agentId.Value, [requiredScope], ct);
        if (!hasScope)
            return StatusCode(403, new ProblemDetails
            {
                Title = "Insufficient scope.",
                Extensions = { ["errorCode"] = "AGENTS.SCOPE_DENIED" },
                Status = 403,
            });

        // Map AgentCommandRequest.CommandType (string) to CommandType enum
        var commandType = Enum.TryParse<CommandType>(request.CommandType, true, out var parsedType)
            ? parsedType : CommandType.RunScript;

        // Persist the command record
        var command = await commandService.CreateCommandAsync(new CreateCommandRequest(
            agentId.Value, commandType, request.Payload,
            request.TimeoutSeconds, MaxRetries: 0), ct);

        // Publish to RabbitMQ so CommandDispatchWorker pushes via SignalR
        await eventPublisher.PublishAsync(new AgentCommandPublishedEvent(
            command.Id, agentId.Value, request.CommandType,
            request.TargetModule, request.Payload,
            request.TimeoutSeconds, command.CreatedAt), ct);

        // Audit
        await agentService.LogActionAsync(
            agentId.Value, null, "command.submitted",
            $"type={request.CommandType}, target={request.TargetModule}",
            202, HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            null, true, null, HttpContext.TraceIdentifier,
            null, $"{{\"commandId\":\"{command.Id}\"}}", ct);

        return Accepted(new AgentCommandResponse(command.Id, command.Status.ToString(), command.CreatedAt));
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
