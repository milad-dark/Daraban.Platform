namespace Daraban.Modules.Inventory.Data.Entities;

/// <summary>
/// Immutable record of a raw inventory submission received from an agent (Task 4.3).
/// The full JSON envelope is stored as-is, then a background processor extracts structured
/// fields (device info, software, network, etc.) into the Asset domain.
/// Append-only: raw submissions are never updated or deleted — they serve as the
/// source of truth for audit and replay.
/// </summary>
public class RawInventorySubmission
{
    /// <summary>Primary key (auto-incrementing long for high-volume append-only tables).</summary>
    public long Id { get; set; }

    /// <summary>Idempotency key: SHA-256 hash of (AgentId + DeviceId + timestampUtc truncated to minute).</summary>
    public string SubmissionHash { get; set; } = default!;

    /// <summary>The agent that submitted this inventory.</summary>
    public Guid AgentId { get; set; }

    /// <summary>The device identifier from the envelope (e.g. hostname, MAC, serial).</summary>
    public string DeviceId { get; set; } = default!;

    /// <summary>The item type from the envelope (e.g. "Computer", "EsxHost", null for network).</summary>
    public string? ItemType { get; set; }

    /// <summary>The action from the envelope (e.g. "inventory", "discovery", "netinventory").</summary>
    public string Action { get; set; } = "inventory";

    /// <summary>Raw JSON payload from the agent's envelope.content field. Stored for replay.</summary>
    public string RawPayload { get; set; } = default!;

    /// <summary>Full envelope JSON for complete audit trail.</summary>
    public string FullEnvelope { get; set; } = default!;

    /// <summary>Processing status: Pending → Processing → Completed / Failed.</summary>
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;

    /// <summary>Error message if processing failed (null while pending/processing).</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Number of device records extracted during processing (0 until completed).</summary>
    public int? DeviceCount { get; set; }

    /// <summary>The entity/node this inventory belongs to (nullable — agents may not have entity scope).</summary>
    public Guid? EntityId { get; set; }

    /// <summary>When the agent sent this submission (from the envelope's timestampUtc).</summary>
    public DateTimeOffset SubmittedAt { get; set; }

    /// <summary>When the server received this submission.</summary>
    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>When background processing started (null until picked up by worker).</summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Source IP address of the submitting request.</summary>
    public string? IpAddress { get; set; }
}

public enum SubmissionStatus
{
    /// <summary>Received, awaiting background processing.</summary>
    Pending = 0,

    /// <summary>Being processed by the inventory worker.</summary>
    Processing = 1,

    /// <summary>Successfully processed — device records extracted.</summary>
    Completed = 2,

    /// <summary>Processing failed (see ErrorMessage for details).</summary>
    Failed = 3,
}
