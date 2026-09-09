using Daraban.Modules.Dashboard.Services.Dtos;
using Daraban.Modules.Identity.Data;
using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Dashboard.Services.Widgets;

// ---- Identity (Agents) widget ----------------------------------------------------
// Agents are global principals (EntityId is nullable) -- the summary therefore aggregates
// across the whole platform rather than per entity. This is intentionally the one widget
// that is not entity-scoped: agent registration is a platform-level concern.

/// <summary>AgentStatusSummary widget: agents by status plus a recent-activity count.</summary>
public sealed class AgentStatusSummaryProvider(IdentityDbContext db) : IWidgetDataProvider
{
    public WidgetType Type => WidgetType.AgentStatusSummary;

    public async Task<WidgetDataDto> GetDataAsync(Guid entityId, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-5);

        var rows = await db.Agents.AsNoTracking()
            .Where(a => !a.IsDeleted)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        long Count(AgentStatus s) => rows.FirstOrDefault(r => r.Status == s)?.Count ?? 0;

        var online = await db.Agents.AsNoTracking()
            .CountAsync(a => !a.IsDeleted && a.LastActiveAt != null && a.LastActiveAt >= cutoff, ct);

        var dto = new AgentStatusSummaryDto(
            Active: Count(AgentStatus.Active),
            Suspended: Count(AgentStatus.Suspended),
            Deactivated: Count(AgentStatus.Deactivated),
            OnlineLast5Minutes: online);

        // Flattened into label/values so the chart widget can render it uniformly.
        var values = new List<LabelValueDto>
        {
            new("active", dto.Active),
            new("suspended", dto.Suspended),
            new("deactivated", dto.Deactivated),
            new("online5m", dto.OnlineLast5Minutes),
        };

        return new WidgetDataDto(WidgetCatalog.ToApiName(Type), values, null);
    }
}
