using Daraban.Modules.Assets.Data;
using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Dashboard.Services.Dtos;
using Daraban.Modules.Inventory.Data;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Dashboard.Services.Widgets;

// ---- Assets + Inventory widgets -------------------------------------------------
// Entity scoping: assets are keyed by EntityNodeId, inventory submissions by EntityId.

/// <summary>AssetCountByType widget: asset counts grouped by asset type.</summary>
public sealed class AssetCountByTypeProvider(AssetsDbContext db) : IWidgetDataProvider
{
    public WidgetType Type => WidgetType.AssetCountByType;

    public async Task<WidgetDataDto> GetDataAsync(Guid entityId, CancellationToken ct)
    {
        var rows = await db.Assets.AsNoTracking()
            .Where(a => a.EntityNodeId == entityId && a.DeletedAt == null)
            .GroupBy(a => a.AssetType.Name)
            .Select(g => new { TypeName = g.Key, Count = g.LongCount() })
            .OrderByDescending(r => r.Count)
            .ToListAsync(ct);

        var values = rows.Select(r => new LabelValueDto(r.TypeName, r.Count)).ToList();
        return new WidgetDataDto(WidgetCatalog.ToApiName(Type), values, null);
    }
}

/// <summary>
/// AssetsNearingEnd widget: assets whose warranty expires within the configured window
/// (default 90 days). The window is injected so ops can tune it without a redeploy.
/// </summary>
public sealed class AssetsNearingEndProvider(AssetsDbContext db, IWidgetOptions options) : IWidgetDataProvider
{
    public WidgetType Type => WidgetType.AssetsNearingEnd;

    public async Task<WidgetDataDto> GetDataAsync(Guid entityId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(options.WarrantyWarningDays);

        var rows = await db.Assets.AsNoTracking()
            .Where(a => a.EntityNodeId == entityId
                        && a.DeletedAt == null
                        && a.WarrantyExpiry != null
                        && a.WarrantyExpiry >= today
                        && a.WarrantyExpiry <= horizon)
            .OrderBy(a => a.WarrantyExpiry)
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.AssetTag,
                TypeName = a.AssetType.Name,
                a.WarrantyExpiry,
            })
            .Take(50) // hard cap: the widget renders a list, not a report
            .ToListAsync(ct);

        var items = rows.Select(a => new AssetNearingEndDto(
            a.Id,
            a.Name,
            a.AssetTag,
            a.TypeName,
            a.WarrantyExpiry,
            a.WarrantyExpiry!.Value.DayNumber - today.DayNumber))
            .ToList<object>();

        return new WidgetDataDto(WidgetCatalog.ToApiName(Type), null, items);
    }
}

/// <summary>RecentInventory widget: latest inventory submissions from agents.</summary>
public sealed class RecentInventoryProvider(InventoryDbContext db) : IWidgetDataProvider
{
    public WidgetType Type => WidgetType.RecentInventory;

    public async Task<WidgetDataDto> GetDataAsync(Guid entityId, CancellationToken ct)
    {
        var rows = await db.RawInventorySubmissions.AsNoTracking()
            .Where(s => s.EntityId == entityId)
            .OrderByDescending(s => s.ReceivedAt)
            .Take(20)
            .Select(s => new RecentInventoryItemDto(
                s.Id,
                s.DeviceId,
                s.ItemType,
                s.Status.ToString(),
                s.DeviceCount,
                s.ReceivedAt))
            .ToListAsync(ct);

        return new WidgetDataDto(WidgetCatalog.ToApiName(Type), null, rows.ToList<object>());
    }
}
