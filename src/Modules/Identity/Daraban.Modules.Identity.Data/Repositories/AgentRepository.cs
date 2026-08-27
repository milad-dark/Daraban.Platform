using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Identity.Data.Repositories;

public class AgentRepository : IAgentRepository
{
    private readonly IdentityDbContext _db;

    public AgentRepository(IdentityDbContext db) => _db = db;

    // ---- Agents ----

    public async Task<Agent?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Agents
            .Include(a => a.Credentials)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Agent?> GetByNameAsync(string name, CancellationToken ct = default)
        => await _db.Agents.FirstOrDefaultAsync(a => a.Name == name, ct);

    public async Task<IReadOnlyList<Agent>> GetPagedAsync(
        Guid? entityId, AgentStatus? status, AgentType? type, string? search,
        int skip, int take, CancellationToken ct = default)
    {
        return await BuildQuery(entityId, status, type, search)
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> GetCountAsync(
        Guid? entityId, AgentStatus? status, AgentType? type, string? search,
        CancellationToken ct = default)
    {
        return await BuildQuery(entityId, status, type, search).CountAsync(ct);
    }

    public void Add(Agent agent) => _db.Agents.Add(agent);
    public void Update(Agent agent) => _db.Agents.Update(agent);

    // ---- Credentials ----

    public async Task<AgentCredential?> GetCredentialByIdAsync(Guid credentialId, CancellationToken ct = default)
        => await _db.AgentCredentials.FindAsync([credentialId], ct);

    public async Task<AgentCredential?> GetCredentialByClientIdAsync(string clientId, CancellationToken ct = default)
        => await _db.AgentCredentials
            .Include(c => c.Agent)
            .FirstOrDefaultAsync(c => c.ClientId == clientId, ct);

    public async Task<IReadOnlyList<AgentCredential>> GetCredentialsByAgentIdAsync(Guid agentId, CancellationToken ct = default)
        => await _db.AgentCredentials
            .Where(c => c.AgentId == agentId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public void AddCredential(AgentCredential credential) => _db.AgentCredentials.Add(credential);
    public void UpdateCredential(AgentCredential credential) => _db.AgentCredentials.Update(credential);

    // ---- Audit ----

    public async Task<IReadOnlyList<AgentAuditLog>> GetAuditLogsAsync(Guid agentId, int skip, int take, CancellationToken ct = default)
        => await _db.AgentAuditLogs
            .Where(l => l.AgentId == agentId)
            .OrderByDescending(l => l.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public async Task<int> GetAuditLogCountAsync(Guid agentId, CancellationToken ct = default)
        => await _db.AgentAuditLogs.CountAsync(l => l.AgentId == agentId, ct);

    public void AddAuditLog(AgentAuditLog logEntry) => _db.AgentAuditLogs.Add(logEntry);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);

    // ---- Private ----

    private IQueryable<Agent> BuildQuery(Guid? entityId, AgentStatus? status, AgentType? type, string? search)
    {
        IQueryable<Agent> q = _db.Agents;

        if (entityId.HasValue)
            q = q.Where(a => a.EntityId == entityId.Value);
        if (status.HasValue)
            q = q.Where(a => a.Status == status.Value);
        if (type.HasValue)
            q = q.Where(a => a.Type == type.Value);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(a => a.Name.Contains(search) || (a.Description != null && a.Description.Contains(search)));

        return q;
    }
}
