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

    // ---- Server-side push methods (called by other services, not by clients) ----

    /// <summary>
    /// Broadcast agent status change to all admin clients and agent-specific subscribers.
    /// Called by AgentControlHub when an agent connects/disconnects, or by services
    /// when agent status is updated.
    /// </summary>
    public async Task NotifyAgentStatusChanged(Guid agentId, string agentName, string newStatus, DateTimeOffset timestamp)
    {
        await Clients.Group("admins").SendAsync("AgentStatusChanged", new
        {
            agentId,
            agentName,
            status = newStatus,
            timestamp,
        });

        await Clients.Group($"agent:{agentId}").SendAsync("AgentStatusChanged", new
        {
            agentId,
            agentName,
            status = newStatus,
            timestamp,
        });
    }

    /// <summary>
    /// Broadcast agent heartbeat to admin clients. Keeps the dashboard alive indicator updated.
    /// </summary>
    public async Task NotifyAgentHeartbeat(Guid agentId, DateTimeOffset timestamp)
    {
        await Clients.Group("admins").SendAsync("AgentHeartbeat", new
        {
            agentId,
            timestamp,
        });
    }

    /// <summary>
    /// Broadcast command completion to admin clients. Powers the CommandPanelComponent live output.
    /// </summary>
    public async Task NotifyCommandCompleted(Guid agentId, Guid commandId, string status, DateTimeOffset completedAt)
    {
        await Clients.Group("admins").SendAsync("CommandCompleted", new
        {
            agentId,
            commandId,
            status,
            completedAt,
        });

        await Clients.Group($"agent:{agentId}").SendAsync("CommandCompleted", new
        {
            agentId,
            commandId,
            status,
            completedAt,
        });
    }
}
