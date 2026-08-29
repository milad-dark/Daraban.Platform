using Daraban.Modules.Identity.Data.Entities;

namespace Daraban.Modules.Identity.Services.Agents;

// ---- Registration / Management ----

public sealed record RegisterAgentRequest(
    string Name,
    string? Description,
    AgentType Type,
    Guid? EntityId,
    string AllowedScopes,
    int RateLimitPerMinute = 0,
    string? Tags = null);

public sealed record UpdateAgentRequest(
    string? Name,
    string? Description,
    AgentStatus? Status,
    string? AllowedScopes,
    int? RateLimitPerMinute,
    string? Tags = null);

public sealed record AgentDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? OwnerUserId,
    Guid? EntityId,
    AgentType Type,
    AgentStatus Status,
    string AllowedScopes,
    int RateLimitPerMinute,
    string? Tags,
    DateTimeOffset? LastActiveAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AgentPagedResult(
    IReadOnlyList<AgentDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

// ---- Credential Management ----

public sealed record CreateCredentialRequest(
    string? Label,
    string? Scopes,
    DateTimeOffset? ExpiresAt = null);

/// <summary>
/// Returned once at creation time. The plain-text secret is NEVER stored or returned again.
/// </summary>
public sealed record CredentialCreatedResponse(
    Guid CredentialId,
    string ClientId,
    string ClientSecret,   // plain text — shown only once
    string? Label,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);

public sealed record CredentialDto(
    Guid Id,
    string ClientId,
    string? Label,
    bool IsActive,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? ExpiresAt,
    string? Scopes,
    DateTimeOffset CreatedAt);

// ---- OAuth2 Client Credentials Token Exchange ----

/// <summary>
/// OAuth2 client_credentials request body (RFC 6749 §4.4.3).
/// </summary>
public sealed record TokenRequest(
    string ClientId,
    string ClientSecret,
    string? Scope = null);

/// <summary>
/// OAuth2 token response (RFC 6749 §5.1).
/// </summary>
public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string? Scope);

// ---- Audit Log ----

public sealed record AuditLogEntry(
    long Id,
    Guid AgentId,
    string Action,
    string? Detail,
    int? HttpStatusCode,
    string? IpAddress,
    long? DurationMs,
    bool Success,
    string? ErrorMessage,
    string? CorrelationId,
    DateTimeOffset Timestamp);

public sealed record AuditLogPagedResult(
    IReadOnlyList<AuditLogEntry> Items,
    int TotalCount,
    int Page,
    int PageSize);

// ---- Agent Command (async via RabbitMQ) ----

public sealed record AgentCommandRequest(
    string CommandType,
    string? TargetModule,
    string? Payload,
    int? TimeoutSeconds = null);

public sealed record AgentCommandResponse(
    Guid CommandId,
    string Status,
    DateTimeOffset QueuedAt);

// ---- Dashboard DTOs (Task 4.5: Agent Dashboard) ----

/// <summary>
/// Agent summary for the list view. Includes online/offline status
/// derived from LastActiveAt vs heartbeat threshold.
/// </summary>
public sealed record AgentListItemDto(
    Guid Id,
    string Name,
    string? Description,
    AgentType Type,
    AgentStatus Status,
    string? Hostname,
    string? OperatingSystem,
    DateTimeOffset? LastActiveAt,
    bool IsOnline,
    int PendingCommandCount,
    int TotalCommandCount,
    DateTimeOffset CreatedAt);

/// <summary>
/// Full agent detail including credential count, command stats, and inventory status.
/// </summary>
public sealed record AgentDetailDto(
    AgentDto Agent,
    int CredentialCount,
    int TotalCommands,
    int CompletedCommands,
    int FailedCommands,
    int PendingCommands,
    DateTimeOffset? LastInventoryAt,
    string? LastInventoryStatus);

/// <summary>
/// Aggregate fleet status for the dashboard overview cards.
/// </summary>
public sealed record AgentFleetSummaryDto(
    int TotalAgents,
    int OnlineAgents,
    int OfflineAgents,
    int SuspendedAgents,
    int TotalCommandsToday,
    int PendingCommands,
    int FailedCommandsLast24h);

/// <summary>
/// Snapshot of the agent's latest inventory submission.
/// </summary>
public sealed record AgentInventorySnapshotDto(
    long SubmissionId,
    string DeviceId,
    string? ItemType,
    string Action,
    string Status,
    int? DeviceCount,
    DateTimeOffset SubmittedAt,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt);

/// <summary>
/// Command history entry for the agent detail view.
/// </summary>
public sealed record AgentCommandHistoryEntry(
    Guid CommandId,
    string CommandType,
    string Status,
    string? Payload,
    int? ExitCode,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    int ExecutionDurationMs);
