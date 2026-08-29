using Daraban.Modules.Identity.Data.Entities;
using Daraban.Platform.Contracts.Agents;

namespace Daraban.Modules.Identity.Data.Repositories;

/// <summary>
/// Repository for remote commands (Task 4.4). Uses the same IdentityDbContext
/// as the agent identity tables — commands are identity-scoped because they
/// are agent-to-server lifecycle objects.
/// </summary>
public interface IAgentCommandRepository
{
    // ---- Write ----
    void AddCommand(AgentCommand command);
    void UpdateCommand(AgentCommand command);
    void AddResult(CommandResult result);

    // ---- Read: Command lifecycle ----
    Task<AgentCommand?> GetCommandByIdAsync(Guid commandId, CancellationToken ct = default);

    /// <summary>
    /// Get queued commands for a specific agent. Used by both:
    /// 1. CommandDispatchWorker to push via SignalR
    /// 2. Agent polling via GET /api/agent/commands/pending
    /// </summary>
    Task<IReadOnlyList<AgentCommand>> GetPendingCommandsAsync(
        Guid agentId, int maxCount, CancellationToken ct = default);

    /// <summary>
    /// Get commands that have exceeded their deadline. Used by Worker.Automation
    /// for timeout detection and retry logic.
    /// </summary>
    Task<IReadOnlyList<AgentCommand>> GetTimedOutCommandsAsync(
        DateTimeOffset now, int maxCount, CancellationToken ct = default);

    // ---- Read: Admin/Reporting ----
    Task<IReadOnlyList<AgentCommand>> GetCommandsByAgentAsync(
        Guid agentId, int skip, int take, CancellationToken ct = default);

    Task<int> GetCommandCountByAgentAsync(Guid agentId, CancellationToken ct = default);

    Task<CommandResult?> GetResultByCommandIdAsync(Guid commandId, CancellationToken ct = default);

    // ---- Write: State transitions ----
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
