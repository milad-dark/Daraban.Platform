using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Daraban.Host.Api.Hubs;

/// <summary>
/// Real-time ticket push channel for the Angular Ticket module (Task 6.5).
/// Mirrors AgentStatusHub's pattern: human admin clients connect with a user JWT,
/// join the "tickets" group for broadcast events, or "ticket:{id}" for one ticket.
///
/// Pushes:
///   - TicketCreated   — a new ticket was filed
///   - TicketUpdated   — status/priority/assignment changed
///   - FollowupAdded   — a followup (TicketTask) was posted; powers the live thread
///   - SlaBreach       — a ticket's due date passed without resolution
///
/// Server-side pushes are issued by services/workers directly through
/// IHubContext&lt;TicketHub&gt; (Clients.Group(...).SendAsync(...)); this hub exposes only
/// subscribe/unsubscribe methods to clients. Authentication: valid user JWT.
/// </summary>
[Authorize]
public class TicketHub(ILogger<TicketHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "tickets");
        logger.LogInformation("Ticket client connected (ConnectionId: {ConnectionId})", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Ticket client disconnected (ConnectionId: {ConnectionId})", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Subscribe to live updates for one ticket (detail view).</summary>
    public async Task SubscribeToTicket(Guid ticketId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket:{ticketId}");
    }

    /// <summary>Unsubscribe from one ticket's updates (leaving the detail view).</summary>
    public async Task UnsubscribeFromTicket(Guid ticketId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket:{ticketId}");
    }

    // ---- Server-side push methods (called via IHubContext<TicketHub>) ----

    public async Task NotifyTicketCreated(Guid ticketId, string title, DateTimeOffset timestamp)
    {
        await Clients.Group("tickets").SendAsync("TicketCreated", new { ticketId, title, timestamp });
    }

    public async Task NotifyTicketUpdated(Guid ticketId, string field, string? oldValue, string? newValue, DateTimeOffset timestamp)
    {
        await Clients.Group("tickets").SendAsync("TicketUpdated", new { ticketId, field, oldValue, newValue, timestamp });
        await Clients.Group($"ticket:{ticketId}").SendAsync("TicketUpdated", new { ticketId, field, oldValue, newValue, timestamp });
    }

    public async Task NotifyFollowupAdded(Guid ticketId, Guid taskId, Guid userId, DateTimeOffset timestamp)
    {
        await Clients.Group("tickets").SendAsync("FollowupAdded", new { ticketId, taskId, userId, timestamp });
        await Clients.Group($"ticket:{ticketId}").SendAsync("FollowupAdded", new { ticketId, taskId, userId, timestamp });
    }

    public async Task NotifySlaBreach(Guid ticketId, DateTimeOffset dueDate, DateTimeOffset timestamp)
    {
        await Clients.Group("tickets").SendAsync("SlaBreach", new { ticketId, dueDate, timestamp });
    }
}
