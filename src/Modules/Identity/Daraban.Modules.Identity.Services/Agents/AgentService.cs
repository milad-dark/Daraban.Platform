using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Platform.Common;
using Daraban.Platform.Contracts.Agents;
using System.Security.Cryptography;
using System.Text;

namespace Daraban.Modules.Identity.Services.Agents;

public class AgentService : IAgentService
{
    private readonly IAgentRepository _repo;
    private readonly IAgentCommandRepository _commandRepo;

    public AgentService(IAgentRepository repo, IAgentCommandRepository commandRepo)
    {
        _repo = repo;
        _commandRepo = commandRepo;
    }

    // ---- Agent CRUD ----

    public async Task<Result<AgentDto>> RegisterAsync(RegisterAgentRequest request, Guid ownerUserId, CancellationToken ct = default)
    {
        var existing = await _repo.GetByNameAsync(request.Name, ct);
        if (existing is not null)
            return Result.Failure<AgentDto>(new Error("AGENTS.NAME_EXISTS", $"An agent named '{request.Name}' already exists.", ErrorType.Conflict));

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            OwnerUserId = ownerUserId,
            EntityId = request.EntityId,
            Type = request.Type,
            Status = AgentStatus.Active,
            AllowedScopes = request.AllowedScopes,
            RateLimitPerMinute = request.RateLimitPerMinute,
            Tags = request.Tags,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _repo.Add(agent);
        await _repo.SaveChangesAsync(ct);

        return Result.Success(MapToDto(agent));
    }

    public async Task<Result<AgentDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var agent = await _repo.GetByIdAsync(id, ct);
        if (agent is null)
            return Result.Failure<AgentDto>(new Error("AGENTS.NOT_FOUND", "Agent not found.", ErrorType.NotFound));

        return Result.Success(MapToDto(agent));
    }

    public async Task<Result<AgentPagedResult>> GetPagedAsync(AgentStatus? status, AgentType? type, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var skip = (page - 1) * pageSize;
        var items = await _repo.GetPagedAsync(null, status, type, search, skip, pageSize, ct);
        var total = await _repo.GetCountAsync(null, status, type, search, ct);

        return Result.Success(new AgentPagedResult(
            items.Select(MapToDto).ToList(),
            total, page, pageSize));
    }

    public async Task<Result<AgentDto>> UpdateAsync(Guid id, UpdateAgentRequest request, CancellationToken ct = default)
    {
        var agent = await _repo.GetByIdAsync(id, ct);
        if (agent is null)
            return Result.Failure<AgentDto>(new Error("AGENTS.NOT_FOUND", "Agent not found.", ErrorType.NotFound));

        if (request.Name is not null)
            agent.Name = request.Name;
        if (request.Description is not null)
            agent.Description = request.Description;
        if (request.Status.HasValue)
            agent.Status = request.Status.Value;
        if (request.AllowedScopes is not null)
            agent.AllowedScopes = request.AllowedScopes;
        if (request.RateLimitPerMinute.HasValue)
            agent.RateLimitPerMinute = request.RateLimitPerMinute.Value;
        if (request.Tags is not null)
            agent.Tags = request.Tags;
        agent.UpdatedAt = DateTimeOffset.UtcNow;

        _repo.Update(agent);
        await _repo.SaveChangesAsync(ct);

        return Result.Success(MapToDto(agent));
    }

    public async Task<Result> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var agent = await _repo.GetByIdAsync(id, ct);
        if (agent is null)
            return Result.Failure(new Error("AGENTS.NOT_FOUND", "Agent not found.", ErrorType.NotFound));

        agent.Status = AgentStatus.Deactivated;
        agent.IsDeleted = true;
        agent.DeletedAt = DateTimeOffset.UtcNow;
        agent.UpdatedAt = DateTimeOffset.UtcNow;

        // Revoke all active credentials
        var credentials = await _repo.GetCredentialsByAgentIdAsync(id, ct);
        foreach (var cred in credentials.Where(c => c.IsActive))
        {
            cred.IsActive = false;
            cred.UpdatedAt = DateTimeOffset.UtcNow;
            _repo.UpdateCredential(cred);
        }

        _repo.Update(agent);
        await _repo.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---- Credential Management ----

    public async Task<Result<CredentialCreatedResponse>> CreateCredentialAsync(Guid agentId, CreateCredentialRequest request, CancellationToken ct = default)
    {
        var agent = await _repo.GetByIdAsync(agentId, ct);
        if (agent is null)
            return Result.Failure<CredentialCreatedResponse>(new Error("AGENTS.NOT_FOUND", "Agent not found.", ErrorType.NotFound));

        if (agent.Status != AgentStatus.Active)
            return Result.Failure<CredentialCreatedResponse>(new Error("AGENTS.NOT_ACTIVE", "Cannot create credentials for a non-active agent.", ErrorType.BusinessRule));

        // Generate client_id and client_secret
        var clientId = $"da_{Guid.NewGuid():N}";
        var clientSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var secretHash = HashSecret(clientSecret);

        var credential = new AgentCredential
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            ClientId = clientId,
            ClientSecretHash = secretHash,
            Label = request.Label,
            Scopes = request.Scopes,
            ExpiresAt = request.ExpiresAt,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _repo.AddCredential(credential);
        await _repo.SaveChangesAsync(ct);

        return Result.Success(new CredentialCreatedResponse(
            credential.Id,
            clientId,
            clientSecret,  // plain text — shown only once
            request.Label,
            request.ExpiresAt,
            credential.CreatedAt));
    }

    public async Task<Result<IReadOnlyList<CredentialDto>>> GetCredentialsAsync(Guid agentId, CancellationToken ct = default)
    {
        var agent = await _repo.GetByIdAsync(agentId, ct);
        if (agent is null)
            return Result.Failure<IReadOnlyList<CredentialDto>>(new Error("AGENTS.NOT_FOUND", "Agent not found.", ErrorType.NotFound));

        var credentials = await _repo.GetCredentialsByAgentIdAsync(agentId, ct);
        var dtos = credentials.Select(c => new CredentialDto(
            c.Id, c.ClientId, c.Label, c.IsActive, c.LastUsedAt, c.ExpiresAt, c.Scopes, c.CreatedAt)).ToList();

        return Result.Success<IReadOnlyList<CredentialDto>>(dtos);
    }

    public async Task<Result> RevokeCredentialAsync(Guid agentId, Guid credentialId, CancellationToken ct = default)
    {
        var credential = await _repo.GetCredentialByIdAsync(credentialId, ct);
        if (credential is null || credential.AgentId != agentId)
            return Result.Failure(new Error("AGENTS.CREDENTIAL_NOT_FOUND", "Credential not found.", ErrorType.NotFound));

        credential.IsActive = false;
        credential.UpdatedAt = DateTimeOffset.UtcNow;
        _repo.UpdateCredential(credential);
        await _repo.SaveChangesAsync(ct);

        return Result.Success();
    }

    // ---- Scope Validation ----

    public async Task<bool> ValidateScopesAsync(Guid agentId, IEnumerable<string> requestedScopes, CancellationToken ct = default)
    {
        var agent = await _repo.GetByIdAsync(agentId, ct);
        if (agent is null || agent.Status != AgentStatus.Active)
            return false;

        var allowed = agent.AllowedScopes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requestedScopes.All(s => allowed.Contains(s) || allowed.Contains("*"));
    }

    // ---- Audit ----

    public async Task LogActionAsync(
        Guid agentId, Guid? credentialId, string action, string? detail,
        int? httpStatusCode, string? ipAddress, string? userAgent,
        long? durationMs, bool success, string? errorMessage,
        string? correlationId, Guid? entityId, string? metadata,
        CancellationToken ct = default)
    {
        var entry = new AgentAuditLog
        {
            Id = 0, // auto-increment
            AgentId = agentId,
            CredentialId = credentialId,
            Action = action,
            Detail = detail,
            HttpStatusCode = httpStatusCode,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DurationMs = durationMs,
            Success = success,
            ErrorMessage = errorMessage,
            CorrelationId = correlationId,
            EntityId = entityId,
            Metadata = metadata,
            Timestamp = DateTimeOffset.UtcNow,
        };

        _repo.AddAuditLog(entry);
        await _repo.SaveChangesAsync(ct);
    }

    public async Task<Result<AuditLogPagedResult>> GetAuditLogAsync(Guid agentId, int page, int pageSize, CancellationToken ct = default)
    {
        var agent = await _repo.GetByIdAsync(agentId, ct);
        if (agent is null)
            return Result.Failure<AuditLogPagedResult>(new Error("AGENTS.NOT_FOUND", "Agent not found.", ErrorType.NotFound));

        var skip = (page - 1) * pageSize;
        var entries = await _repo.GetAuditLogsAsync(agentId, skip, pageSize, ct);
        var total = await _repo.GetAuditLogCountAsync(agentId, ct);

        var dtos = entries.Select(e => new AuditLogEntry(
            e.Id, e.AgentId, e.Action, e.Detail, e.HttpStatusCode, e.IpAddress,
            e.DurationMs, e.Success, e.ErrorMessage, e.CorrelationId, e.Timestamp)).ToList();

        return Result.Success(new AuditLogPagedResult(dtos, total, page, pageSize));
    }

    // ---- Session / Activity ----

    public async Task TouchLastActiveAsync(Guid agentId, CancellationToken ct = default)
    {
        var agent = await _repo.GetByIdAsync(agentId, ct);
        if (agent is null)
            return;

        agent.LastActiveAt = DateTimeOffset.UtcNow;
        _repo.Update(agent);
        await _repo.SaveChangesAsync(ct);
    }

    // ---- Dashboard (Task 4.5) ----

    private const int DefaultHeartbeatThresholdMinutes = 5;

    public async Task<IReadOnlyList<AgentListItemDto>> GetAgentListAsync(
        AgentStatus? status, AgentType? type, string? search,
        int page, int pageSize, CancellationToken ct = default)
    {
        var skip = (page - 1) * pageSize;
        var agents = await _repo.GetPagedAsync(null, status, type, search, skip, pageSize, ct);
        var threshold = DateTimeOffset.UtcNow.AddMinutes(-DefaultHeartbeatThresholdMinutes);

        // Batch-fetch pending command counts per agent (1 query, not N)
        var agentIds = agents.Select(a => a.Id).ToList();
        var pendingCounts = await _commandRepo.GetPendingCountByAgentIdsAsync(agentIds, ct);

        var items = new List<AgentListItemDto>(agents.Count);
        foreach (var a in agents)
        {
            pendingCounts.TryGetValue(a.Id, out var pendingCount);
            items.Add(new AgentListItemDto(
                a.Id, a.Name, a.Description, a.Type, a.Status,
                Hostname: null, // populated by inventory data if available
                OperatingSystem: null,
                a.LastActiveAt,
                IsOnline: a.LastActiveAt.HasValue && a.LastActiveAt > threshold,
                PendingCommandCount: pendingCount,
                TotalCommandCount: 0, // total not needed for list view
                a.CreatedAt));
        }

        return items;
    }

    public async Task<int> GetAgentListCountAsync(
        AgentStatus? status, AgentType? type, string? search, CancellationToken ct = default)
        => await _repo.GetCountAsync(null, status, type, search, ct);

    public async Task<AgentDetailDto?> GetAgentDetailAsync(Guid agentId, CancellationToken ct = default)
    {
        var agent = await _repo.GetByIdAsync(agentId, ct);
        if (agent is null) return null;

        var credentials = await _repo.GetCredentialsByAgentIdAsync(agentId, ct);
        // Single aggregate query instead of fetching all commands
        var stats = await _commandRepo.GetAggregateStatsAsync(
            agentIds: [agentId], ct: ct);

        return new AgentDetailDto(
            Agent: MapToDto(agent),
            CredentialCount: credentials.Count(c => c.IsActive),
            TotalCommands: stats.TotalCommands,
            CompletedCommands: stats.CompletedCommands,
            FailedCommands: stats.FailedCommands,
            PendingCommands: stats.PendingCommands,
            LastInventoryAt: null, // populated by controller with inventory data
            LastInventoryStatus: null);
    }

    public async Task<AgentFleetSummaryDto> GetFleetSummaryAsync(CancellationToken ct = default)
    {
        var allAgents = await _repo.GetPagedAsync(null, null, null, null, 0, 10000, ct);
        var threshold = DateTimeOffset.UtcNow.AddMinutes(-DefaultHeartbeatThresholdMinutes);
        var now = DateTimeOffset.UtcNow;

        // Single aggregate query instead of N+1 per-agent command queries
        var todayStats = await _commandRepo.GetAggregateStatsAsync(since: now.Date, ct: ct);
        var last24hStats = await _commandRepo.GetAggregateStatsAsync(since: now.AddHours(-24), ct: ct);
        var pendingStats = await _commandRepo.GetAggregateStatsAsync(ct: ct);

        return new AgentFleetSummaryDto(
            TotalAgents: allAgents.Count,
            OnlineAgents: allAgents.Count(a => a.LastActiveAt.HasValue && a.LastActiveAt > threshold && a.Status == AgentStatus.Active),
            OfflineAgents: allAgents.Count(a => (!a.LastActiveAt.HasValue || a.LastActiveAt <= threshold) && a.Status == AgentStatus.Active),
            SuspendedAgents: allAgents.Count(a => a.Status == AgentStatus.Suspended),
            TotalCommandsToday: todayStats.TotalCommands,
            PendingCommands: pendingStats.PendingCommands,
            FailedCommandsLast24h: last24hStats.FailedCommands);
    }

    public async Task<IReadOnlyList<AgentCommandHistoryEntry>> GetCommandHistoryAsync(
        Guid agentId, int page, int pageSize, CancellationToken ct = default)
    {
        var skip = (page - 1) * pageSize;
        var commands = await _commandRepo.GetCommandsByAgentAsync(agentId, skip, pageSize, ct);

        // Batch-fetch results (1 query, not N)
        var commandIds = commands.Select(c => c.Id).ToList();
        var results = await _commandRepo.GetResultsByCommandIdsAsync(commandIds, ct);

        var entries = new List<AgentCommandHistoryEntry>(commands.Count);
        foreach (var c in commands)
        {
            results.TryGetValue(c.Id, out var result);
            entries.Add(new AgentCommandHistoryEntry(
                c.Id, c.CommandType.ToString(), c.Status.ToString(), c.Payload,
                result?.ExitCode, c.LastError,
                c.CreatedAt, c.CompletedAt,
                result?.ExecutionDurationMs ?? 0));
        }

        return entries;
    }

    // ---- Helpers ----

    internal static AgentDto MapToDto(Agent agent) => new(
        agent.Id, agent.Name, agent.Description, agent.OwnerUserId, agent.EntityId,
        agent.Type, agent.Status, agent.AllowedScopes, agent.RateLimitPerMinute,
        agent.Tags, agent.LastActiveAt, agent.CreatedAt, agent.UpdatedAt);

    internal static string HashSecret(string secret)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
