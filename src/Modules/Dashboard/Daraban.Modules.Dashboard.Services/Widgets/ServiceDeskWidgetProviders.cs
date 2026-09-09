using Daraban.Modules.Dashboard.Services.Dtos;
using Daraban.Modules.ServiceDesk.Data;
using Daraban.Modules.ServiceDesk.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Dashboard.Services.Widgets;

// ---- ServiceDesk widgets -------------------------------------------------------
// All queries are AsNoTracking and entity-scoped (OWASP A01: cross-tenant data can never
// leak through the dashboard). Aggregations run server-side -- only aggregates cross the wire.

/// <summary>OpenTicketsCount widget: total open tickets for the entity.</summary>
public sealed class OpenTicketsCountProvider(ServiceDeskDbContext db) : IWidgetDataProvider
{
    public WidgetType Type => WidgetType.OpenTicketsCount;

    public async Task<WidgetDataDto> GetDataAsync(Guid entityId, CancellationToken ct)
    {
        var open = TicketStatuses.Open;
        var count = await db.Tickets.AsNoTracking()
            .CountAsync(t => t.EntityId == entityId && open.Contains(t.Status), ct);

        return new WidgetDataDto(WidgetCatalog.ToApiName(Type),
            [new LabelValueDto("open", count)], null);
    }
}

/// <summary>TicketsByStatus widget: ticket distribution across ITIL statuses.</summary>
public sealed class TicketsByStatusProvider(ServiceDeskDbContext db) : IWidgetDataProvider
{
    public WidgetType Type => WidgetType.TicketsByStatus;

    public async Task<WidgetDataDto> GetDataAsync(Guid entityId, CancellationToken ct)
    {
        var rows = await db.Tickets.AsNoTracking()
            .Where(t => t.EntityId == entityId)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        var values = rows
            .OrderBy(r => (int)r.Status)
            .Select(r => new LabelValueDto(r.Status.ToString(), r.Count))
            .ToList();

        return new WidgetDataDto(WidgetCatalog.ToApiName(Type), values, null);
    }
}

/// <summary>TicketsByPriority widget: open tickets grouped by priority.</summary>
public sealed class TicketsByPriorityProvider(ServiceDeskDbContext db) : IWidgetDataProvider
{
    public WidgetType Type => WidgetType.TicketsByPriority;

    public async Task<WidgetDataDto> GetDataAsync(Guid entityId, CancellationToken ct)
    {
        var open = TicketStatuses.Open;
        var rows = await db.Tickets.AsNoTracking()
            .Where(t => t.EntityId == entityId && open.Contains(t.Status))
            .GroupBy(t => t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        var values = rows
            .OrderBy(r => (int)r.Priority)
            .Select(r => new LabelValueDto(r.Priority.ToString(), r.Count))
            .ToList();

        return new WidgetDataDto(WidgetCatalog.ToApiName(Type), values, null);
    }
}
