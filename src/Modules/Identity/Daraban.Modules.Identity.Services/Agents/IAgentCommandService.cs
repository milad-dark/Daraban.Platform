using Daraban.Platform.Contracts.Agents;

namespace Daraban.Modules.Identity.Services.Agents;

/// <summary>
/// Service for managing remote command lifecycle (Task 4.4).
/// Handles command creation, status transitions, timeout detection, and retry logic.
/// </summary>
public interface IAgentCommandService
{
    /// <summary>
    /// Creates a new command for an agent. Transitions status to Queued.
    /// Called by the admin API when an admin triggers a command.
    /// </summary>
    Task<CommandDto> CreateCommandAsync(CreateCommandRequest request, CancellationToken ct = default);

    /// <summary>
    /// Get pending commands for a specific agent (Created or Queued status).
    /// Used by both SignalR dispatch and agent HTTP polling.
    /// </summary>
    Task<IReadOnlyList<PendingCommandDto>> GetPendingCommandsAsync(
        Guid agentId, int maxCount = 10, CancellationToken ct = default);

    /// <summary>
    /// Agent acknowledges receipt of a command. Transitions: Queued → Received.
    /// </summary>
    Task<bool> AcknowledgeCommandAsync(Guid agentId, Guid commandId, CancellationToken ct = default);

    /// <summary>
    /// Agent reports the result of executing a command.
    /// Transitions: Received/Executing → Completed or Failed.
    /// </summary>
    Task<CommandResultResponse> ReportResultAsync(
        Guid agentId, Guid commandId, CommandResultRequest request, CancellationToken ct = default);

    /// <summary>
    /// Mark a command as dispatched. Transitions: Queued → Dispatched.
    /// Sets DeadlineAt based on TimeoutSeconds.
    /// </summary>
    Task MarkDispatchedAsync(Guid commandId, CancellationToken ct = default);

    /// <summary>
    /// Check for timed-out commands and transition them to TimedOut.
    /// If retries remain, re-queues for retry. Returns timed-out commands
    /// that exceeded max retries (now permanently Failed).
    /// </summary>
    Task<IReadOnlyList<CommandDto>> ProcessTimedOutCommandsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get full command details. Used by admin endpoints and result checking.
    /// </summary>
    Task<CommandDto?> GetCommandAsync(Guid commandId, CancellationToken ct = default);

    /// <summary>
    /// Get paged command history for a specific agent. Used by admin UI.
    /// </summary>
    Task<(IReadOnlyList<CommandDto> Items, int TotalCount)> GetCommandsByAgentAsync(
        Guid agentId, int page, int pageSize, CancellationToken ct = default);
}
