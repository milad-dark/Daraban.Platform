namespace Daraban.Modules.Identity.Data.Entities;

/// <summary>
/// Immutable audit log entry for every agent action. Append-only — never updated or deleted.
/// Enables full traceability of what an agent did, when, and from where (Task 4.1 SS4).
/// </summary>
public class AgentAuditLog
{
    public long Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid? CredentialId { get; set; }

    /// <summary>Category of action: "auth", "api_call", "event_publish", "command", etc.</summary>
    public string Action { get; set; } = default!;

    /// <summary>Optional sub-action (e.g. "GET /api/v1/assets", "inventory.submit").</summary>
    public string? Detail { get; set; }

    /// <summary>HTTP status code if this was an API call, or NULL for non-HTTP actions.</summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>Source IP address of the request.</summary>
    public string? IpAddress { get; set; }

    /// <summary>User-Agent header value.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Duration of the operation in milliseconds.</summary>
    public long? DurationMs { get; set; }

    /// <summary>True if the action succeeded; false for errors/forbidden.</summary>
    public bool Success { get; set; } = true;

    /// <summary>Error message if Success is false.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Request correlation ID for distributed tracing.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>The entity/node context this action was performed under.</summary>
    public Guid? EntityId { get; set; }

    /// <summary>Arbitrary JSON metadata for the action (e.g. payload size, affected record count).</summary>
    public string? Metadata { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
