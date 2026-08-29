using Daraban.Modules.Identity.Services.Agents;
using Daraban.Platform.Contracts.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Daraban.Host.AgentApi.Hubs;

/// <summary>
/// Server → Agent push channel (Task 4.1 SS2.3).
/// Agents connect here after authenticating to receive:
///   - Command dispatches (the server pushes work items to the agent)
///   - Configuration updates (scope changes, rate limit changes)
///   - Graceful shutdown signals (agent should finish current work and disconnect)
///
/// Authentication: agents connect with their JWT (issued via /api/v1/agents/auth/token).
/// The hub validates the "is_agent" claim and uses the "sub" claim as the agent identity.
/// Group membership: each agent is automatically added to a group named by its agent ID,
/// so the server can push to a specific agent via Clients.Group(agentId).
/// </summary>
[Authorize(Policy = "agent:scope:commands:submit")]
public class AgentControlHub : Hub
{
    private readonly ILogger<AgentControlHub> _logger;
    private readonly IAgentCommandService _commandService;
    private readonly IAgentService _agentService;

    public AgentControlHub(ILogger<AgentControlHub> logger, IAgentCommandService commandService, IAgentService agentService)
    {
        _logger = logger;
        _commandService = commandService;
        _agentService = agentService;
    }

    public override async Task OnConnectedAsync()
    {
        var agentId = GetAgentId();
        if (agentId is null)
        {
            _logger.LogWarning("AgentControlHub: non-agent client attempted connection");
            Context.Abort();
            return;
        }

        // Add this connection to the agent's group for targeted push
        // agentId is guaranteed non-null here by the null-check + early return above
        await Groups.AddToGroupAsync(Context.ConnectionId, agentId.Value.ToString());
        _logger.LogInformation("Agent {AgentId} connected (ConnectionId: {ConnectionId})", agentId, Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var agentId = GetAgentId();
        if (agentId.HasValue)
            _logger.LogInformation("Agent {AgentId} disconnected (ConnectionId: {ConnectionId}, Error: {Error})",
                agentId, Context.ConnectionId, exception?.Message);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Agent confirms receipt of a command. The server uses this to update command status.
    /// </summary>
    public async Task AcknowledgeCommand(Guid commandId)
    {
        var agentId = GetAgentId();
        if (agentId is null) return;

        var ok = await _commandService.AcknowledgeCommandAsync(agentId.Value, commandId);
        _logger.LogInformation("Agent {AgentId} acknowledged command {CommandId}: {Ok}", agentId, commandId, ok ? "ok" : "not-found");

        if (ok)
            await Clients.Caller.SendAsync("CommandAcknowledged", commandId);
    }

    /// <summary>
    /// Agent reports command completion with result data.
    /// </summary>
    public async Task ReportCommandResult(Guid commandId, bool success, string? resultPayload, string? errorMessage)
    {
        var agentId = GetAgentId();
        if (agentId is null) return;

        var request = new CommandResultRequest(
            Success: success,
            Output: resultPayload,
            ErrorMessage: errorMessage,
            ExitCode: null,
            ExecutionDurationMs: 0);

        try
        {
            var response = await _commandService.ReportResultAsync(agentId.Value, commandId, request);
            _logger.LogInformation("Agent {AgentId} reported command {CommandId}: {Status}",
                agentId, commandId, response.Status);

            // Notify all SignalR clients (Angular UI) of the command completion
            await Clients.All.SendAsync("CommandCompleted", new
            {
                commandId,
                agentId = agentId.Value,
                status = response.Status.ToString(),
                receivedAt = response.ReceivedAt,
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Agent {AgentId} reported unknown command {CommandId}: {Error}",
                agentId, commandId, ex.Message);
        }
    }

    /// <summary>
    /// Server pushes a command to a specific agent via SignalR.
    /// Called by CommandDispatchWorker after picking up a queued command.
    /// </summary>
    public async Task SendCommandToAgent(Guid agentId, PendingCommandDto command)
    {
        await Clients.Group(agentId.ToString()).SendAsync("ReceiveCommand", command);
    }

    /// <summary>
    /// Agent sends a heartbeat. Server updates last-active timestamp.
    /// </summary>
    public async Task Heartbeat()
    {
        var agentId = GetAgentId();
        if (agentId is null) return;

        await _agentService.TouchLastActiveAsync(agentId.Value);
        await Clients.Caller.SendAsync("HeartbeatAck", DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Extracts the agent ID from the JWT "sub" claim, only if "is_agent" claim is present.
    /// </summary>
    private Guid? GetAgentId()
    {
        var isAgent = Context.User?.FindFirst("is_agent")?.Value;
        if (isAgent != "true") return null;

        var sub = Context.User?.FindFirst("sub")?.Value;
        return sub is not null ? Guid.Parse(sub) : null;
    }
}
