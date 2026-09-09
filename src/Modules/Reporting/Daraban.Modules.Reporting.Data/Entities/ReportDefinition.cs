using System.Text.Json;
using Daraban.Platform.Common;

namespace Daraban.Modules.Reporting.Data.Entities;

/// <summary>
/// A reusable report template (Task 7.2): which module's dataset to query, which columns to
/// include, and how to render the output. The module/format are constrained by the closed
/// ReportCatalog in the Services layer -- arbitrary free-text never reaches a query builder.
/// </summary>
public class ReportDefinition : TenantScopedEntity
{
    /// <summary>Display name; unique per entity scope.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Catalog key of the dataset to report on (e.g. "TicketsByStatus").</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Serialized filter dictionary, e.g. {"status":"open","days":30}. JSONB so new
    /// filter keys can be introduced without schema migrations.</summary>
    public string FiltersJson { get; set; } = "{}";

    /// <summary>Serialized ordered column list; empty array means "all default columns".</summary>
    public string ColumnsJson { get; set; } = "[]";

    /// <summary>Export format: "csv" (always), "xlsx" (ClosedXML, MIT), "pdf" (QuestPDF, MIT).</summary>
    public string Format { get; set; } = "csv";

    /// <summary>Cron expression for scheduled generation; null = manual generation only.</summary>
    public string? Schedule { get; set; }

    /// <summary>Whether scheduled generation is active.</summary>
    public bool ScheduleEnabled { get; set; }

    /// <summary>Who requested scheduled deliveries / owns the definition.</summary>
    public Guid? OwnerUserId { get; set; }

    // ---- Parsed accessors (not mapped) --------------------------------------------

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        MaxDepth = 8,
    };

    public IReadOnlyDictionary<string, string> ParseFilters()
    {
        if (string.IsNullOrWhiteSpace(FiltersJson)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(FiltersJson, JsonOptions)
                   ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    public IReadOnlyList<string> ParseColumns()
    {
        if (string.IsNullOrWhiteSpace(ColumnsJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(ColumnsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string SerializeFilters(IReadOnlyDictionary<string, string> filters) =>
        JsonSerializer.Serialize(filters, JsonOptions);

    public static string SerializeColumns(IReadOnlyList<string> columns) =>
        JsonSerializer.Serialize(columns, JsonOptions);
}
