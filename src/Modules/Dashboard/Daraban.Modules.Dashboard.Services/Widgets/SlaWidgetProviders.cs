using Daraban.Modules.Dashboard.Services.Dtos;
using Daraban.Modules.ServiceDesk.Data;
using Daraban.Modules.ServiceDesk.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Dashboard.Services.Widgets;

// ---- SLA widget -------------------------------------------------------------------

/// <summary>
/// SlaComplianceRate widget: percentage of tickets resolved on or before their SLA due date,
/// over a rolling window (default 30 days). Solved + Closed tickets with a due date count as
/// the denominator; those meeting the date are the numerator.
/// </summary>
public sealed class SlaComplianceRateProvider(ServiceDeskDbContext db, IWidgetOptions options)
    : IWidgetDataProvider
{
    public WidgetType Type => WidgetType.SlaComplianceRate;

    public async Task<WidgetDataDto> GetDataAsync(Guid entityId, CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-options.SlaWindowDays);

        var resolved = await db.Tickets.AsNoTracking()
            .Where(t => t.EntityId == entityId
                        && t.DueDate != null
                        && t.OpenedAt >= since
                        && (t.SolvedAt != null || t.ClosedAt != null))
            .Select(t => new { t.DueDate, t.SolvedAt, t.ClosedAt })
            .ToListAsync(ct);

        // Denominator zero -> report 100% rather than NaN/0: an SLA with no resolved tickets
        // has not been violated, and a NaN would break JSON serialization.
        if (resolved.Count == 0)
        {
            return new WidgetDataDto(WidgetCatalog.ToApiName(Type),
                [new LabelValueDto("compliance", 100)], null);
        }

        var met = resolved.Count(t => (t.SolvedAt ?? t.ClosedAt)!.Value <= t.DueDate!.Value);
        var rate = (long)Math.Round(met * 100.0 / resolved.Count);

        return new WidgetDataDto(WidgetCatalog.ToApiName(Type),
            [new LabelValueDto("compliance", rate)], null);
    }
}
