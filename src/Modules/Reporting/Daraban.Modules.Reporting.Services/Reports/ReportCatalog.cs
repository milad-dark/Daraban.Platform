namespace Daraban.Modules.Reporting.Services.Reports;

/// <summary>
/// The closed set of reportable datasets the platform ships (Task 7.2). Deliberately static
/// and enumerable: a definition's Module must be one of these keys and its Columns a subset
/// of the matching entry, so arbitrary strings can never reach a query builder or a renderer
/// (same shape as the Dashboard module's WidgetCatalog).
/// </summary>
public static class ReportCatalog
{
    /// <summary>Metadata for one reportable dataset.</summary>
    public sealed record ReportDefinitionMetadata(
        string Key,
        string Name,
        string Description,
        IReadOnlyList<ReportColumn> Columns,
        IReadOnlyList<string> SupportedFilters);

    /// <summary>One column of a dataset: key used in definitions + human-readable header.</summary>
    public sealed record ReportColumn(string Key, string Header, ReportColumnKind Kind);

    public enum ReportColumnKind { Text, Number, Date, Boolean }

    /// <summary>Well-known filter keys understood by the providers below.</summary>
    public static class Filters
    {
        public const string Status = "status";
        public const string Days = "days";
    }

    /// <summary>Ordered catalog -- the same order GET /api/v1/reports/definitions serves.</summary>
    public static readonly IReadOnlyList<ReportDefinitionMetadata> All =
    [
        new(
            "Tickets",
            "Tickets",
            "All service desk tickets with status, priority and SLA columns.",
            [
                new("id", "Id", ReportColumnKind.Text),
                new("title", "Title", ReportColumnKind.Text),
                new("type", "Type", ReportColumnKind.Text),
                new("status", "Status", ReportColumnKind.Text),
                new("priority", "Priority", ReportColumnKind.Text),
                new("openedAt", "Opened At", ReportColumnKind.Date),
                new("solvedAt", "Solved At", ReportColumnKind.Date),
                new("closedAt", "Closed At", ReportColumnKind.Date),
                new("slaMet", "SLA Met", ReportColumnKind.Boolean),
                new("satisfaction", "Satisfaction", ReportColumnKind.Number),
            ],
            ["status", "days"]),

        new(
            "Assets",
            "Assets",
            "All assets with type, location, purchase and warranty columns.",
            [
                new("id", "Id", ReportColumnKind.Text),
                new("name", "Name", ReportColumnKind.Text),
                new("assetTag", "Asset Tag", ReportColumnKind.Text),
                new("typeName", "Type", ReportColumnKind.Text),
                new("locationName", "Location", ReportColumnKind.Text),
                new("status", "Status", ReportColumnKind.Text),
                new("purchaseDate", "Purchase Date", ReportColumnKind.Date),
                new("purchaseCost", "Purchase Cost", ReportColumnKind.Number),
                new("warrantyExpiry", "Warranty Expiry", ReportColumnKind.Date),
            ],
            ["status"]),

        new(
            "Agents",
            "Agents",
            "Registered agents with status, type and last-activity columns.",
            [
                new("id", "Id", ReportColumnKind.Text),
                new("name", "Name", ReportColumnKind.Text),
                new("typeName", "Type", ReportColumnKind.Text),
                new("status", "Status", ReportColumnKind.Text),
                new("lastActiveAt", "Last Active", ReportColumnKind.Date),
                new("ownerName", "Owner", ReportColumnKind.Text),
            ],
            ["status"]),
    ];

    private static readonly Dictionary<string, ReportDefinitionMetadata> ByKey =
        All.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Case-insensitive lookup; false for anything outside the catalog.</summary>
    public static bool TryGet(string? key, out ReportDefinitionMetadata? metadata)
    {
        metadata = null;
        if (string.IsNullOrWhiteSpace(key)) return false;
        if (!ByKey.TryGetValue(key.Trim(), out var found)) return false;
        metadata = found;
        return true;
    }

    /// <summary>Validates that every requested column key exists in the dataset.</summary>
    public static bool ColumnsAreValid(ReportDefinitionMetadata metadata, IEnumerable<string> columns) =>
        columns.All(c => metadata.Columns.Any(col => col.Key.Equals(c, StringComparison.OrdinalIgnoreCase)));
}
