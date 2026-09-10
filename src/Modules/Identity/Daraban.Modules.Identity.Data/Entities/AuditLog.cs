namespace Daraban.Modules.Identity.Data.Entities;

/// <summary>
/// Immutable, append-only audit record for every tracked entity change (Task 7.3).
/// Written by <see cref="Auditing.AuditLogSaveChangesInterceptor"/> inside the same
/// transaction as the change it describes, so an audit row never exists without its
/// change and vice versa. Never updated or deleted by application code.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>CLR type name of the changed entity (e.g. "User", "Asset").</summary>
    public string EntityType { get; set; } = default!;

    /// <summary>Primary key of the changed entity row.</summary>
    public Guid EntityId { get; set; }

    /// <summary>"Added", "Modified" or "Deleted" (EF Core EntityState names).</summary>
    public string Action { get; set; } = default!;

    /// <summary>User that performed the change; null for system/worker/anonymous changes.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>JSON object of previous values; null for inserts.</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON object of resulting values; null for deletes.</summary>
    public string? NewValues { get; set; }

    /// <summary>Client IP address of the request, when the change came over HTTP.</summary>
    public string? IpAddress { get; set; }

    /// <summary>User-Agent header of the request, when the change came over HTTP.</summary>
    public string? UserAgent { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
