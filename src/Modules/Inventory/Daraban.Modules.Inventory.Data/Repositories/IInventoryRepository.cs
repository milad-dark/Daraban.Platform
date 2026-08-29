using Daraban.Modules.Inventory.Data.Entities;

namespace Daraban.Modules.Inventory.Data.Repositories;

/// <summary>
/// Data access for raw inventory submissions (Task 4.3).
/// Append-only: Add is the only mutation; everything else is read or status update.
/// </summary>
public interface IInventoryRepository
{
    /// <summary>Add a new raw submission (always succeeds — append-only).</summary>
    void Add(RawInventorySubmission submission);

    /// <summary>Find a submission by its idempotency hash for the same agent.</summary>
    Task<RawInventorySubmission?> GetByHashAsync(string hash, Guid agentId, CancellationToken ct = default);

    /// <summary>Find a submission by ID, scoped to a specific agent.</summary>
    Task<RawInventorySubmission?> GetByIdAsync(long id, Guid agentId, CancellationToken ct = default);

    /// <summary>List submissions for an agent, newest first.</summary>
    Task<IReadOnlyList<RawInventorySubmission>> ListAsync(
        Guid agentId, int skip, int take, CancellationToken ct = default);

    /// <summary>Count submissions for an agent.</summary>
    Task<int> GetCountAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>Get the most recent submission for an agent (inventory snapshot).</summary>
    Task<RawInventorySubmission?> GetLatestByAgentIdAsync(Guid agentId, CancellationToken ct = default);

    /// <summary>Get the most recent submission for multiple agents (batch query for dashboard list).</summary>
    Task<IReadOnlyDictionary<Guid, RawInventorySubmission>> GetLatestByAgentIdsAsync(
        IEnumerable<Guid> agentIds, CancellationToken ct = default);

    /// <summary>Find a submission by ID without agentId scoping (trusted internal use).
    /// Used by background worker which has no agent context.</summary>
    Task<RawInventorySubmission?> GetByIdUnscopedAsync(long id, CancellationToken ct = default);

    /// <summary>Update submission status. Used by background worker.</summary>
    void Update(RawInventorySubmission submission);

    /// <summary>Save all pending changes.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
