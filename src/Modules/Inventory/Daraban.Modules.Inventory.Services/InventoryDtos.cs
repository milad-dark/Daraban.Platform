namespace Daraban.Modules.Inventory.Services;

// ── Agent Envelope DTOs ──────────────────────────────────────────────────────
// These match the exact JSON shape sent by DarabanClient.SendEnvelopeAsync().
// Property names are camelCase (System.Text.Json default) matching the Agent's
// JsonSerializerOptions which uses JsonNamingPolicy.CamelCase.

/// <summary>
/// The envelope wrapping every POST from the Agent (Task 4.3).
/// Exact match for DarabanClient.SendEnvelopeAsync() output.
/// </summary>
public sealed record AgentEnvelope
{
    /// <summary>Device identifier (hostname, MAC, serial — depends on collector type).</summary>
    public string DeviceId { get; init; } = default!;

    /// <summary>Item type: "Computer", "EsxHost", null for discovery/network.</summary>
    public string? ItemType { get; init; }

    /// <summary>Action: "inventory", "discovery", "netinventory", "esx", "wakeonlan".</summary>
    public string Action { get; init; } = "inventory";

    /// <summary>When the agent captured this data (UTC).</summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>The actual payload — shape depends on Action. May be a nested object or array.</summary>
    public object? Content { get; init; }
}

// ── Submission DTOs ──────────────────────────────────────────────────────────

/// <summary>
/// Result returned to the agent after a successful submission (HTTP 202 Accepted).
/// Uses long to match the DB primary key — no Guid conversion needed.
/// </summary>
public sealed record SubmissionAcceptedResponse(
    long SubmissionId,
    string Status,
    DateTimeOffset ReceivedAt);

/// <summary>
/// Status response for a pending/completed submission (agent polling).
/// </summary>
public sealed record SubmissionStatusResponse(
    long SubmissionId,
    string Status,
    int? DeviceCount,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt,
    string? ErrorMessage);

/// <summary>
/// Paged list of submissions for an agent (management UI / admin).
/// </summary>
public sealed record SubmissionPagedResult(
    IReadOnlyList<SubmissionDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record SubmissionDto(
    long Id,
    Guid AgentId,
    string DeviceId,
    string? ItemType,
    string Action,
    string Status,
    int? DeviceCount,
    DateTimeOffset SubmittedAt,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt,
    string? ErrorMessage);

// ── Processing DTOs ─────────────────────────────────────────────────────────

/// <summary>
/// Parsed device info extracted from raw inventory content.
/// Used by the background processor to create/update Asset records.
/// </summary>
public sealed record ParsedDeviceInfo(
    string DeviceId,
    string? ComputerName,
    string? OperatingSystem,
    string? OsArchitecture,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? BiosVersion,
    string? BiosReleaseDate,
    int? CpuCount,
    string? CpuName,
    long? TotalMemoryBytes,
    int? StorageCount,
    string? MacAddress,
    string? IpAddress,
    string? Domain,
    string? LoggedOnUser,
    string? RawContentJson);
