namespace Daraban.Modules.Dashboard.Services.Widgets;

/// <summary>
/// The closed set of widget types the platform ships (Task 7.1). Deliberately an enum, not a
/// free string: the value flows into provider resolution, and an enum makes the API surface
/// enumerable and impossible to probe with arbitrary input (OWASP A01/A03 hygiene).
/// </summary>
public enum WidgetType
{
    OpenTicketsCount = 1,
    TicketsByStatus = 2,
    TicketsByPriority = 3,
    AssetCountByType = 4,
    AssetsNearingEnd = 5,
    RecentInventory = 6,
    SlaComplianceRate = 7,
    AgentStatusSummary = 8,
}

/// <summary>Static widget catalog served by GET /api/v1/dashboard/widgets.</summary>
public static class WidgetCatalog
{
    /// <summary>Metadata for one widget type in the dashboard designer.</summary>
    public sealed record WidgetDefinition(
        WidgetType Type,
        string Name,
        string Description,
        string DefaultSizeW,
        string DefaultSizeH);

    /// <summary>Ordered catalog -- the same order the frontend offers in "Add widget".</summary>
    public static readonly IReadOnlyList<WidgetDefinition> All =
    [
        new(WidgetType.OpenTicketsCount, "Open Tickets",
            "Total count of tickets a technician still owes work on.", "3", "1"),
        new(WidgetType.TicketsByStatus, "Tickets by Status",
            "Distribution of tickets across the ITIL workflow statuses.", "3", "3"),
        new(WidgetType.TicketsByPriority, "Tickets by Priority",
            "Open tickets grouped by priority, from Low to Critical.", "3", "3"),
        new(WidgetType.AssetCountByType, "Assets by Type",
            "Asset counts grouped by asset type.", "3", "3"),
        new(WidgetType.AssetsNearingEnd, "Warranty Ending",
            "Assets whose warranty expires within the next 90 days.", "3", "2"),
        new(WidgetType.RecentInventory, "Recent Inventory",
            "Latest inventory submissions received from agents.", "3", "3"),
        new(WidgetType.SlaComplianceRate, "SLA Compliance",
            "Percentage of resolved tickets met before their SLA due date.", "3", "1"),
        new(WidgetType.AgentStatusSummary, "Agent Status",
            "Registered agents by status, with recently-active count.", "3", "2"),
    ];

    private static readonly Dictionary<string, WidgetType> NamesByName =
        All.ToDictionary(w => w.Name.Replace(" ", string.Empty), w => w.Type);

    /// <summary>Case-insensitive lookup of a widget type from an API route value.
    /// Returns false for anything outside the catalog -- callers must not guess.</summary>
    public static bool TryParse(string? name, out WidgetType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(name)) return false;
        return NamesByName.TryGetValue(name.Trim().Replace(" ", string.Empty), out type);
    }

    public static string ToApiName(WidgetType type) =>
        All.First(w => w.Type == type).Name.Replace(" ", string.Empty);
}
