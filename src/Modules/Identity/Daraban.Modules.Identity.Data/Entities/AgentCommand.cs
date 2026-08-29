using Daraban.Platform.Contracts.Agents;
using Daraban.Platform.Common;

namespace Daraban.Modules.Identity.Data.Entities;

/// <summary>
/// A remote command dispatched to an agent. Created by an admin, queued via RabbitMQ,
/// and executed by the agent. Tracks the full lifecycle: Created → Queued → Dispatched →
/// Received → Executing → Completed/Failed/TimedOut.
/// </summary>
public class AgentCommand : BaseEntity
{
    /// <summary>The agent this command is addressed to.</summary>
    public Guid AgentId { get; set; }

    /// <summary>Command type (RunScript, InstallSoftware, etc.).</summary>
    public CommandType CommandType { get; set; }

    /// <summary>Current lifecycle state.</summary>
    public CommandStatus Status { get; set; } = CommandStatus.Created;

    /// <summary>Command-specific payload (script content, package name, service name, etc.).</summary>
    public string? Payload { get; set; }

    /// <summary>
    /// Maximum seconds the agent has to complete execution. NULL means no timeout.
    /// Default is 300 (5 minutes) for most command types.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>Number of times this command has been retried.</summary>
    public int RetryCount { get; set; }

    /// <summary>Maximum retries allowed before marking as permanently Failed.</summary>
    public int MaxRetries { get; set; }

    /// <summary>Error message from the most recent execution attempt.</summary>
    public string? LastError { get; set; }

    /// <summary>Exit code from the most recent execution (null if not yet executed).</summary>
    public int? ExitCode { get; set; }

    /// <summary>When the command was created by the admin.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Last status change timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When the command was dispatched to the agent. NULL until dispatched.</summary>
    public DateTimeOffset? DispatchedAt { get; set; }

    /// <summary>When the agent acknowledged receiving the command. NULL until received.</summary>
    public DateTimeOffset? ReceivedAt { get; set; }

    /// <summary>When the command completed, failed, or timed out. NULL until terminal.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Maximum time the agent has from DispatchedAt before timeout. NULL = no limit.</summary>
    public DateTimeOffset? DeadlineAt { get; set; }
}

/// <summary>
/// Stores the full output of a command execution. Separated from AgentCommand
/// because output can be large (multi-MB for script runs) and should not
/// bloat the command table.
/// </summary>
public class CommandResult
{
    public long Id { get; set; }

    /// <summary>Reference to the parent command.</summary>
    public Guid CommandId { get; set; }

    /// <summary>The agent that executed this command.</summary>
    public Guid AgentId { get; set; }

    /// <summary>Whether execution succeeded (exit code 0 or agent-reported success).</summary>
    public bool Success { get; set; }

    /// <summary>Full stdout/stderr output from script execution.</summary>
    public string? Output { get; set; }

    /// <summary>Error message if execution failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Process exit code (null if not applicable to command type).</summary>
    public int? ExitCode { get; set; }

    /// <summary>Actual wall-clock execution time in milliseconds.</summary>
    public int ExecutionDurationMs { get; set; }

    /// <summary>When the result was received by the server.</summary>
    public DateTimeOffset ReceivedAt { get; set; }
}
