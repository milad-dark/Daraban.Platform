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
[Authorize]
public class AgentControlHub : Hub
{
    private readonly ILogger<AgentControlHub> _logger;

    public AgentControlHub(ILogger<AgentControlHub> logger) => _logger = logger;

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

        _logger.LogInformation("Agent {AgentId} acknowledged command {CommandId}", agentId, commandId);
        // TODO: Update command status in DB, publish AgentCommandAcknowledgedEvent
        await Task.CompletedTask;
    }

    /// <summary>
    /// Agent reports command completion with result data.
    /// </summary>
    public async Task ReportCommandResult(Guid commandId, bool success, string? resultPayload, string? errorMessage)
    {
        var agentId = GetAgentId();
        if (agentId is null) return;

        _logger.LogInformation("Agent {AgentId} reported command {CommandId} result: {Success}",
            agentId, commandId, success ? "success" : $"failure: {errorMessage}");
        // TODO: Update command status, publish AgentCommandCompletedEvent
        await Task.CompletedTask;
    }

    /// <summary>
    /// Agent sends a heartbeat. Server updates last-active timestamp.
    /// </summary>
    public async Task Heartbeat()
    {
        var agentId = GetAgentId();
        if (agentId is null) return;

        // TODO: Call IAgentService.TouchLastActiveAsync(agentId.Value)
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
