namespace Daraban.Platform.Contracts.Agents;

// ── Command Types ─────────────────────────────────────────────────────────────

/// <summary>
/// Initial set of command types supported by the remote command system.
/// Each type maps to a specific execution behavior on the agent side.
/// </summary>
public enum CommandType
{
    /// <summary>Execute a shell script and return stdout/stderr.</summary>
    RunScript = 0,

    /// <summary>Install software via the platform's package manager.</summary>
    InstallSoftware = 1,

    /// <summary>Uninstall software.</summary>
    UninstallSoftware = 2,

    /// <summary>Restart a Windows/Linux service by name.</summary>
    RestartService = 3,

    /// <summary>Reboot the device.</summary>
    RebootDevice = 4,

    /// <summary>Force immediate inventory collection (overrides schedule).</summary>
    CollectInventoryNow = 5,
}

/// <summary>
/// State machine for command lifecycle: Created → Queued → Dispatched →
/// Received → Executing → Completed/Failed/TimedOut. Retry may re-enter
/// from any terminal state back to Queued.
/// </summary>
public enum CommandStatus
{
    Created = 0,
    Queued = 1,
    Dispatched = 2,
    Received = 3,
    Executing = 4,
    Completed = 5,
    Failed = 6,
    TimedOut = 7,
    Cancelled = 8,
}

// ── Events ────────────────────────────────────────────────────────────────────

/// <summary>
/// Published when a command transitions to Completed or Failed.
/// Consumers: Angular UI (via SignalR), Audit (command.completion metric),
/// Notifications (admin alert on failure).
/// </summary>
public sealed record AgentCommandCompletedEvent(
    Guid CommandId,
    Guid AgentId,
    CommandType CommandType,
    CommandStatus FinalStatus,
    int? ExitCode,
    DateTimeOffset CompletedAt);

/// <summary>
/// Published when a command exceeds its timeout and is marked TimedOut.
/// Consumers: Worker.Automation retries if retries remain, else marks Failed.
/// </summary>
public sealed record AgentCommandTimedOutEvent(
    Guid CommandId,
    Guid AgentId,
    CommandType CommandType,
    DateTimeOffset TimedOutAt);

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <summary>
/// A pending command for an agent to execute. Returned by GET /api/agent/commands/pending.
/// </summary>
public sealed record PendingCommandDto(
    Guid CommandId,
    CommandType CommandType,
    string? Payload,
    int? TimeoutSeconds,
    DateTimeOffset CreatedAt);

/// <summary>
/// The result of a command execution. Posted by the agent to
/// POST /api/agent/commands/{commandId}/result.
/// </summary>
public sealed record CommandResultRequest(
    bool Success,
    string? Output,
    string? ErrorMessage,
    int? ExitCode,
    int ExecutionDurationMs);

/// <summary>
/// Response after a command result is accepted.
/// </summary>
public sealed record CommandResultResponse(
    Guid CommandId,
    CommandStatus Status,
    DateTimeOffset ReceivedAt);

// ── Admin DTOs (for Host.Api admin endpoints) ─────────────────────────────────

/// <summary>
/// Request body for an admin creating a new command to send to an agent.
/// </summary>
public sealed record CreateCommandRequest(
    Guid AgentId,
    CommandType CommandType,
    string? Payload,
    int? TimeoutSeconds = null,
    int MaxRetries = 0);

/// <summary>
/// Full command detail returned by admin endpoints.
/// </summary>
public sealed record CommandDto(
    Guid Id,
    Guid AgentId,
    CommandType CommandType,
    CommandStatus Status,
    string? Payload,
    int? TimeoutSeconds,
    int RetryCount,
    int MaxRetries,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DispatchedAt,
    DateTimeOffset? CompletedAt);
