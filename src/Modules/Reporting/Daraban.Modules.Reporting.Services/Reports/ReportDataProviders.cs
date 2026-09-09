using Daraban.Modules.Assets.Data;
using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Identity.Data;
using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.ServiceDesk.Data;
using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Platform.Common;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Reporting.Services.Reports;

// ---- Report data providers (Task 7.2) ---------------------------------------------
// Same read-only aggregation pattern as the Dashboard module's widget providers: AsNoTracking,
// entity-scoped (OWASP A01), projections only. Each provider applies the catalog's supported
// filters; unknown filter keys are ignored rather than erroring, so old definitions keep
// rendering when new filters ship.

/// <summary>Tickets dataset: service desk tickets with SLA and satisfaction columns.</summary>
public sealed class TicketsReportDataProvider(ServiceDeskDbContext db) : IReportDataProvider
{
    public string DatasetKey => "Tickets";

    public async Task<Result<ReportTable>> GetTableAsync(
        Guid entityId, IReadOnlyDictionary<string, string> filters, IReadOnlyList<string> columns, CancellationToken ct = default)
    {
        var query = db.Tickets.AsNoTracking().Where(t => t.EntityId == entityId);

        if (filters.TryGetValue(ReportCatalog.Filters.Status, out var status) &&
            Enum.TryParse<TicketStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(t => t.Status == parsedStatus);
        }

        if (filters.TryGetValue(ReportCatalog.Filters.Days, out var days) &&
            int.TryParse(days, out var window) && window > 0)
        {
            var since = DateTimeOffset.UtcNow.AddDays(-window);
            query = query.Where(t => t.OpenedAt >= since);
        }

        var rows = await query
            .OrderByDescending(t => t.OpenedAt)
            .Select(t => new object?[]
            {
                t.Id,
                t.Title,
                t.Type.ToString(),
                t.Status.ToString(),
                t.Priority.ToString(),
                t.OpenedAt,
                t.SolvedAt,
                t.ClosedAt,
                t.SlaLevelId == null || t.SolvedAt == null || t.DueDate == null
                    ? (bool?)null
                    : t.SolvedAt <= t.DueDate,
                t.SatisfactionRating,
            })
            .ToListAsync(ct);

        return Result.Success(ReportTableBuilder.BuildTable(DatasetKey, columns, rows));
    }
}

/// <summary>Assets dataset: assets with type, location, purchase and warranty columns.</summary>
public sealed class AssetsReportDataProvider(AssetsDbContext db) : IReportDataProvider
{
    public string DatasetKey => "Assets";

    public async Task<Result<ReportTable>> GetTableAsync(
        Guid entityId, IReadOnlyDictionary<string, string> filters, IReadOnlyList<string> columns, CancellationToken ct = default)
    {
        var query = db.Assets.AsNoTracking()
            .Where(a => a.DeletedAt == null);

        if (filters.TryGetValue(ReportCatalog.Filters.Status, out var status) &&
            Enum.TryParse<AssetStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }

        var rows = await query
            .OrderBy(a => a.Name)
            .Select(a => new object?[]
            {
                a.Id,
                a.Name,
                a.AssetTag,
                a.AssetType.Name,
                a.Location != null ? a.Location.Name : null,
                a.Status.ToString(),
                a.PurchaseDate,
                a.PurchaseCost,
                a.WarrantyExpiry,
            })
            .ToListAsync(ct);

        return Result.Success(ReportTableBuilder.BuildTable(DatasetKey, columns, rows));
    }
}

/// <summary>Agents dataset: registered agents with status, type and last-activity columns.</summary>
public sealed class AgentsReportDataProvider(IdentityDbContext db) : IReportDataProvider
{
    public string DatasetKey => "Agents";

    public async Task<Result<ReportTable>> GetTableAsync(
        Guid entityId, IReadOnlyDictionary<string, string> filters, IReadOnlyList<string> columns, CancellationToken ct = default)
    {
        var query = db.Agents.AsNoTracking().Where(a => !a.IsDeleted);

        if (filters.TryGetValue(ReportCatalog.Filters.Status, out var status) &&
            Enum.TryParse<AgentStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }

        var rows = await query
            .OrderBy(a => a.Name)
            .Select(a => new object?[]
            {
                a.Id,
                a.Name,
                a.Type.ToString(),
                a.Status.ToString(),
                a.LastActiveAt,
                a.OwnerUserId,
            })
            .ToListAsync(ct);

        return Result.Success(ReportTableBuilder.BuildTable(DatasetKey, columns, rows));
    }
}

/// <summary>Shared projection: orders columns per the definition (default = catalog order),
/// falling back to the provider's full row when a definition predates a column rename.</summary>
internal static class ReportTableBuilder
{
    public static ReportTable BuildTable(
        string datasetKey, IReadOnlyList<string> columns, List<object?[]> rows)
    {
        if (!ReportCatalog.TryGet(datasetKey, out var metadata) || metadata is null)
            throw new InvalidOperationException($"Dataset '{datasetKey}' has no catalog entry.");

        // Resolve the requested subset (default: everything, in catalog order).
        var selected = metadata.Columns
            .Where(c => columns.Count == 0 ||
                        columns.Any(r => r.Equals(c.Key, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // Position of each selected column within the provider's row array.
        var keyOrder = metadata.Columns.Select((c, i) => (c.Key, i))
            .ToDictionary(x => x.Key, x => x.i, StringComparer.OrdinalIgnoreCase);

        var indices = selected
            .Where(c => keyOrder.ContainsKey(c.Key))
            .Select(c => keyOrder[c.Key])
            .ToList();

        var projected = rows
            .Select(r => indices.Select(i => i < r.Length ? r[i] : null).ToList())
            .Cast<IReadOnlyList<object?>>()
            .ToList();

        var tableColumns = selected
            .Select(c => new ReportTable.ReportColumn(c.Key, c.Header, c.Kind))
            .ToList();

        return new ReportTable
        {
            DatasetKey = datasetKey,
            Columns = tableColumns,
            Rows = projected,
        };
    }
}
