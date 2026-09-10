using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Identity.Data.Repositories;

public interface IAuditLogRepository
{
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPagedAsync(
        string? entityType,
        Guid? entityId,
        Guid? actorUserId,
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken ct = default);

    Task<IReadOnlyList<AuditLog>> GetEntityHistoryAsync(
        string entityType,
        Guid entityId,
        int take,
        CancellationToken ct = default);
}

/// <summary>
/// Read-only access to the append-only audit trail (Task 7.3). Deliberately exposes no
/// Add/Update/Delete -- rows are only ever written by
/// <see cref="Auditing.AuditLogSaveChangesInterceptor"/>, keeping the trail tamper-evident
/// from application code.
/// </summary>
public class AuditLogRepository : IAuditLogRepository
{
    private readonly IdentityDbContext _db;

    public AuditLogRepository(IdentityDbContext db) => _db = db;

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPagedAsync(
        string? entityType,
        Guid? entityId,
        Guid? actorUserId,
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = BuildQuery(entityType, entityId, actorUserId, action, from, to);

        var items = await query
            .OrderByDescending(l => l.OccurredAt)
            .ThenByDescending(l => l.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        var total = await query.CountAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<AuditLog>> GetEntityHistoryAsync(
        string entityType,
        Guid entityId,
        int take,
        CancellationToken ct = default)
        => await _db.AuditLogs
            .AsNoTracking()
            .Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .OrderByDescending(l => l.OccurredAt)
            .ThenByDescending(l => l.Id)
            .Take(take)
            .ToListAsync(ct);

    private IQueryable<AuditLog> BuildQuery(
        string? entityType,
        Guid? entityId,
        Guid? actorUserId,
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        IQueryable<AuditLog> query = _db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(l => l.EntityType == entityType);
        if (entityId.HasValue)
            query = query.Where(l => l.EntityId == entityId.Value);
        if (actorUserId.HasValue)
            query = query.Where(l => l.ActorUserId == actorUserId.Value);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(l => l.Action == action);
        if (from.HasValue)
            query = query.Where(l => l.OccurredAt >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.OccurredAt <= to.Value);

        return query;
    }
}
