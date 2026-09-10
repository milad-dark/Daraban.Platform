using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Platform.Common;

namespace Daraban.Modules.Identity.Services.Audit;

// ---- DTOs (Task 7.3) ------------------------------------------------------------------

/// <summary>One row of the audit browser. Old/new JSON values ride along for the diff viewer.</summary>
public sealed record AuditLogDto(
    long Id,
    string EntityType,
    Guid EntityId,
    string Action,
    Guid? ActorUserId,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset OccurredAt,
    string? OldValues,
    string? NewValues);

/// <summary>Page envelope matching the platform's page-based response convention.</summary>
public sealed record AuditLogPagedResult(
    IReadOnlyList<AuditLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

// ---- Service ---------------------------------------------------------------------------

public interface IAuditLogService
{
    /// <summary>Paged, filtered audit trail for the browser component.</summary>
    Task<Result<AuditLogPagedResult>> SearchAsync(
        string? entityType,
        Guid? entityId,
        Guid? actorUserId,
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Full change history for one record, newest first (inline history panel).</summary>
    Task<Result<IReadOnlyList<AuditLogDto>>> GetEntityHistoryAsync(
        string entityType,
        Guid entityId,
        int limit,
        CancellationToken ct = default);
}

/// <summary>
/// Read-only query service over the audit trail (Task 7.3). All input validation happens
/// here so the controller stays a thin adapter; page-size caps protect the database from
/// unbounded reads of an append-only table.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;
    private const int MaxHistoryLimit = 100;

    private readonly IAuditLogRepository _repository;

    public AuditLogService(IAuditLogRepository repository) => _repository = repository;

    public async Task<Result<AuditLogPagedResult>> SearchAsync(
        string? entityType,
        Guid? entityId,
        Guid? actorUserId,
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // Action filter values are validated against the EF state names -- an unknown
        // value can never match a row, so returning a validated error is cheaper and
        // clearer than a silently empty page.
        var allowedActions = new[] { "Added", "Modified", "Deleted" };
        if (!string.IsNullOrWhiteSpace(action) && !allowedActions.Contains(action, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure<AuditLogPagedResult>(new Error(
                "AUDIT.INVALID_ACTION",
                $"Unknown action '{action}'. Allowed: {string.Join(", ", allowedActions)}.",
                ErrorType.Validation));
        }

        if (from.HasValue && to.HasValue && from > to)
        {
            return Result.Failure<AuditLogPagedResult>(new Error(
                "AUDIT.INVALID_RANGE",
                "'from' must be earlier than or equal to 'to'.",
                ErrorType.Validation));
        }

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize < 1
            ? DefaultPageSize
            : Math.Min(pageSize, MaxPageSize);

        var (items, total) = await _repository.GetPagedAsync(
            entityType,
            entityId,
            actorUserId,
            action,
            from,
            to,
            (normalizedPage - 1) * normalizedPageSize,
            normalizedPageSize,
            ct);

        var dtos = items.Select(e => new AuditLogDto(
            e.Id,
            e.EntityType,
            e.EntityId,
            e.Action,
            e.ActorUserId,
            e.IpAddress,
            e.UserAgent,
            e.OccurredAt,
            e.OldValues,
            e.NewValues)).ToList();

        return Result.Success(new AuditLogPagedResult(dtos, total, normalizedPage, normalizedPageSize));
    }

    public async Task<Result<IReadOnlyList<AuditLogDto>>> GetEntityHistoryAsync(
        string entityType,
        Guid entityId,
        int limit,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return Result.Failure<IReadOnlyList<AuditLogDto>>(new Error(
                "AUDIT.INVALID_ENTITY_TYPE",
                "Entity type is required.",
                ErrorType.Validation));
        }

        var cappedLimit = limit < 1 ? MaxHistoryLimit : Math.Min(limit, MaxHistoryLimit);

        var entries = await _repository.GetEntityHistoryAsync(entityType.Trim(), entityId, cappedLimit, ct);

        var dtos = entries.Select(e => new AuditLogDto(
            e.Id,
            e.EntityType,
            e.EntityId,
            e.Action,
            e.ActorUserId,
            e.IpAddress,
            e.UserAgent,
            e.OccurredAt,
            e.OldValues,
            e.NewValues)).ToList();

        return Result.Success<IReadOnlyList<AuditLogDto>>(dtos);
    }
}
