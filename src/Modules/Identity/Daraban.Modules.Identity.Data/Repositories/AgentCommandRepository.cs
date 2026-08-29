using Daraban.Modules.Identity.Data.Entities;
using Daraban.Platform.Contracts.Agents;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Identity.Data.Repositories;

public class AgentCommandRepository(IdentityDbContext db) : IAgentCommandRepository
{

    // ---- Write ----

    public void AddCommand(AgentCommand command) => db.AgentCommands.Add(command);

    public void UpdateCommand(AgentCommand command) => db.AgentCommands.Update(command);

    public void AddResult(CommandResult result) => db.CommandResults.Add(result);

    // ---- Read: Command lifecycle ----

    public async Task<AgentCommand?> GetCommandByIdAsync(Guid commandId, CancellationToken ct = default)
        => await db.AgentCommands.FindAsync([commandId], ct);

    public async Task<IReadOnlyList<AgentCommand>> GetPendingCommandsAsync(
        Guid agentId, int maxCount, CancellationToken ct = default)
    {
        // Commands in Created or Queued status are pending dispatch.
        // Ordered by CreatedAt (FIFO) — oldest commands first.
        return await db.AgentCommands
            .Where(c => c.AgentId == agentId &&
                        (c.Status == CommandStatus.Created || c.Status == CommandStatus.Queued))
            .OrderBy(c => c.CreatedAt)
            .Take(maxCount)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AgentCommand>> GetTimedOutCommandsAsync(
        DateTimeOffset now, int maxCount, CancellationToken ct = default)
    {
        // Commands that are dispatched/executing and have passed their deadline.
        return await db.AgentCommands
            .Where(c => (c.Status == CommandStatus.Dispatched ||
                         c.Status == CommandStatus.Received ||
                         c.Status == CommandStatus.Executing) &&
                        c.DeadlineAt != null && c.DeadlineAt <= now)
            .OrderBy(c => c.DeadlineAt)
            .Take(maxCount)
            .ToListAsync(ct);
    }

    // ---- Read: Admin/Reporting ----

    public async Task<IReadOnlyList<AgentCommand>> GetCommandsByAgentAsync(
        Guid agentId, int skip, int take, CancellationToken ct = default)
    {
        return await db.AgentCommands
            .Where(c => c.AgentId == agentId)
            .OrderByDescending(c => c.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> GetCommandCountByAgentAsync(Guid agentId, CancellationToken ct = default)
        => await db.AgentCommands.CountAsync(c => c.AgentId == agentId, ct);

    public async Task<CommandResult?> GetResultByCommandIdAsync(Guid commandId, CancellationToken ct = default)
        => await db.CommandResults.FirstOrDefaultAsync(r => r.CommandId == commandId, ct);

    public async Task<IReadOnlyDictionary<Guid, CommandResult>> GetResultsByCommandIdsAsync(
        IEnumerable<Guid> commandIds, CancellationToken ct = default)
    {
        var ids = commandIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, CommandResult>();

        var results = await db.CommandResults
            .Where(r => ids.Contains(r.CommandId))
            .ToListAsync(ct);

        return results.ToDictionary(r => r.CommandId, r => r);
    }

    // ---- Aggregate stats (dashboard) ----

    public async Task<CommandAggregateStats> GetAggregateStatsAsync(
        IEnumerable<Guid>? agentIds = null, DateTimeOffset? since = null, CancellationToken ct = default)
    {
        var query = db.AgentCommands.AsQueryable();

        if (agentIds is not null)
        {
            var ids = agentIds.ToList();
            if (ids.Count > 0)
                query = query.Where(c => ids.Contains(c.AgentId));
        }

        if (since.HasValue)
            query = query.Where(c => c.CreatedAt >= since.Value);

        return new CommandAggregateStats(
            TotalCommands: await query.CountAsync(ct),
            CompletedCommands: await query.CountAsync(c => c.Status == CommandStatus.Completed, ct),
            FailedCommands: await query.CountAsync(c => c.Status == CommandStatus.Failed, ct),
            PendingCommands: await query.CountAsync(c => c.Status == CommandStatus.Queued || c.Status == CommandStatus.Dispatched, ct));
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetPendingCountByAgentIdsAsync(
        IEnumerable<Guid> agentIds, CancellationToken ct = default)
    {
        var ids = agentIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, int>();

        var pendingCounts = await db.AgentCommands
            .Where(c => ids.Contains(c.AgentId) &&
                        (c.Status == CommandStatus.Queued || c.Status == CommandStatus.Dispatched))
            .GroupBy(c => c.AgentId)
            .Select(g => new { AgentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AgentId, x => x.Count, ct);

        // Ensure all agent IDs have an entry (even if 0 pending)
        var result = new Dictionary<Guid, int>(ids.Count);
        foreach (var id in ids)
            result[id] = pendingCounts.GetValueOrDefault(id, 0);

        return result;
    }

    // ---- Write: State transitions ----

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await db.SaveChangesAsync(ct);
}
