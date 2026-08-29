using Daraban.Modules.Identity.Data.Entities;
using Daraban.Platform.Common;

namespace Daraban.Modules.Identity.Services.Agents;

/// <summary>
/// Agent management operations — CRUD, credential rotation, audit trail.
/// Used by both the AgentApi host (agent self-management) and the main Api host (admin operations).
/// </summary>
public interface IAgentService
{
    // ---- Agent CRUD ----
    Task<Result<AgentDto>> RegisterAsync(RegisterAgentRequest request, Guid ownerUserId, CancellationToken ct = default);
    Task<Result<AgentDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<AgentPagedResult>> GetPagedAsync(
        AgentStatus? status, AgentType? type, string? search,
        int page, int pageSize, CancellationToken ct = default);
    Task<Result<AgentDto>> UpdateAsync(Guid id, UpdateAgentRequest request, CancellationToken ct = default);
    Task<Result> DeactivateAsync(Guid id, CancellationToken ct = default);

    // ---- Credential Management ----
    Task<Result<CredentialCreatedResponse>> CreateCredentialAsync(
        Guid agentId, CreateCredentialRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CredentialDto>>> GetCredentialsAsync(Guid agentId, CancellationToken ct = default);
    Task<Result> RevokeCredentialAsync(Guid agentId, Guid credentialId, CancellationToken ct = default);

    // ---- Scope Validation ----
    Task<bool> ValidateScopesAsync(Guid agentId, IEnumerable<string> requestedScopes, CancellationToken ct = default);

    // ---- Audit ----
    Task LogActionAsync(Guid agentId, Guid? credentialId, string action, string? detail,
        int? httpStatusCode, string? ipAddress, string? userAgent,
        long? durationMs, bool success, string? errorMessage,
        string? correlationId, Guid? entityId, string? metadata,
        CancellationToken ct = default);
    Task<Result<AuditLogPagedResult>> GetAuditLogAsync(
        Guid agentId, int page, int pageSize, CancellationToken ct = default);

    // ---- Session / Activity ----
    Task TouchLastActiveAsync(Guid agentId, CancellationToken ct = default);

    // ---- Dashboard (Task 4.5) ----
    Task<IReadOnlyList<AgentListItemDto>> GetAgentListAsync(
        AgentStatus? status, AgentType? type, string? search,
        int page, int pageSize, CancellationToken ct = default);
    Task<int> GetAgentListCountAsync(
        AgentStatus? status, AgentType? type, string? search, CancellationToken ct = default);
    Task<AgentDetailDto?> GetAgentDetailAsync(Guid agentId, CancellationToken ct = default);
    Task<AgentFleetSummaryDto> GetFleetSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AgentCommandHistoryEntry>> GetCommandHistoryAsync(
        Guid agentId, int page, int pageSize, CancellationToken ct = default);
}
