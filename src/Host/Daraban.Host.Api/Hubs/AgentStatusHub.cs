using Daraban.Modules.Identity.Services.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Daraban.Host.Api.Hubs;

/// <summary>
/// Real-time agent status push channel for the Angular dashboard (Task 4.5).
/// This hub is for HUMAN ADMIN clients (Angular UI), NOT for agents.
/// Agents use AgentControlHub on the AgentApi host.
///
/// Pushes:
///   - AgentStatusChanged  — when an agent goes online/offline/status change
///   - AgentHeartbeat      — when an agent sends a heartbeat (keep-alive)
///   - CommandCompleted    — when a command finishes (success/failure)
///   - FleetSummaryUpdate  — periodic fleet summary refresh
///
/// Authentication: valid user JWT (admin panel session).
/// Server-side broadcasts are issued by server code via IHubContext&lt;AgentStatusHub&gt;;
/// the methods below are client-facing queries/subscriptions only.
/// </summary>
[Authorize]
public class AgentStatusHub(ILogger<AgentStatusHub> logger, IAgentService agentService) : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Admin clients join the "admins" group for broadcast updates
        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        logger.LogInformation("Admin client connected (ConnectionId: {ConnectionId})", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Admin client disconnected (ConnectionId: {ConnectionId}, Error: {Error})",
            Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Admin requests current fleet summary. Returns the summary directly to the caller.
    /// </summary>
    public async Task<object> GetFleetSummary()
    {
        var summary = await agentService.GetFleetSummaryAsync();
        return new
        {
            summary.TotalAgents,
            summary.OnlineAgents,
            summary.OfflineAgents,
            summary.SuspendedAgents,
            summary.TotalCommandsToday,
            summary.PendingCommands,
            summary.FailedCommandsLast24h,
        };
    }

    /// <summary>
    /// Admin subscribes to status updates for a specific agent.
    /// Pushes AgentStatusChanged events for that agent only.
    /// </summary>
    public async Task SubscribeToAgent(Guid agentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"agent:{agentId}");
        logger.LogDebug("Admin subscribed to agent {AgentId} updates", agentId);
    }

    /// <summary>
    /// Admin unsubscribes from a specific agent's updates.
    /// </summary>
    public async Task UnsubscribeFromAgent(Guid agentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"agent:{agentId}");
        logger.LogDebug("Admin unsubscribed from agent {AgentId} updates", agentId);
    }
}
