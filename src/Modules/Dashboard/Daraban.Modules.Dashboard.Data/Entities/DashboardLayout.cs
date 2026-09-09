using System.Text.Json;
using Daraban.Platform.Common;

namespace Daraban.Modules.Dashboard.Data.Entities;

/// <summary>
/// A user's saved dashboard layout (Task 7.1). One row per user per dashboard -- the JSON
/// payload is the ordered list of widgets with their grid position/size. Stored as JSONB so
/// new widget metadata (e.g. custom titles) can be added without schema migrations.
/// </summary>
public class DashboardLayout : BaseEntity
{
    /// <summary>Owner of this layout. Layouts are strictly personal -- no entity sharing.</summary>
    public Guid UserId { get; set; }

    /// <summary>Unique per user (currently one dashboard page; the name readies multi-page).</summary>
    public string Name { get; set; } = "default";

    /// <summary>Serialized widget grid: [{ widgetType, x, y, w, h }] in CSS-grid coordinates.</summary>
    public string LayoutJson { get; set; } = "[]";

    // ---- Parsed accessor (not mapped) --------------------------------------------
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        MaxDepth = 8,
    };

    /// <summary>Deserializes the stored JSON; returns an empty grid on corrupt payloads so a
    /// single bad row can never break a user's dashboard load.</summary>
    public IReadOnlyList<WidgetPlacement> ParseLayout()
    {
        if (string.IsNullOrWhiteSpace(LayoutJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<WidgetPlacement>>(LayoutJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>Position of one widget on the dashboard grid.</summary>
public class WidgetPlacement
{
    public string WidgetType { get; set; } = string.Empty;
    /// <summary>Grid column (0-based).</summary>
    public int X { get; set; }
    /// <summary>Grid row (0-based).</summary>
    public int Y { get; set; }
    /// <summary>Width in grid units.</summary>
    public int W { get; set; } = 4;
    /// <summary>Height in grid units.</summary>
    public int H { get; set; } = 2;
}
