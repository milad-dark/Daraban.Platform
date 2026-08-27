using Daraban.Modules.Identity.Data.Entities;

namespace Daraban.Modules.Identity.Data.Repositories;

public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Agent?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Agent>> GetPagedAsync(
        Guid? entityId, AgentStatus? status, AgentType? type, string? search,
        int skip, int take, CancellationToken ct = default);
    Task<int> GetCountAsync(Guid? entityId, AgentStatus? status, AgentType? type, string? search, CancellationToken ct = default);
    void Add(Agent agent);
    void Update(Agent agent);

    // ---- Credentials ----
    Task<AgentCredential?> GetCredentialByIdAsync(Guid credentialId, CancellationToken ct = default);
    Task<AgentCredential?> GetCredentialByClientIdAsync(string clientId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentCredential>> GetCredentialsByAgentIdAsync(Guid agentId, CancellationToken ct = default);
    void AddCredential(AgentCredential credential);
    void UpdateCredential(AgentCredential credential);

    // ---- Audit ----
    Task<IReadOnlyList<AgentAuditLog>> GetAuditLogsAsync(Guid agentId, int skip, int take, CancellationToken ct = default);
    Task<int> GetAuditLogCountAsync(Guid agentId, CancellationToken ct = default);
    void AddAuditLog(AgentAuditLog logEntry);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
