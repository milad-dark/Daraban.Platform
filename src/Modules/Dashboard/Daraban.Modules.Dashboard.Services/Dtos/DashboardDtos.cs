using Daraban.Modules.Dashboard.Services.Widgets;

namespace Daraban.Modules.Dashboard.Services.Dtos;

// ---- Layout DTOs --------------------------------------------------------------

/// <summary>One widget placement as exposed over the API.</summary>
public sealed record WidgetPlacementDto(string WidgetType, int X, int Y, int W, int H);

/// <summary>The user's complete dashboard layout.</summary>
public sealed record DashboardLayoutDto(IReadOnlyList<WidgetPlacementDto> Widgets, DateTimeOffset UpdatedAt);

/// <summary>Request body for PUT /api/v1/dashboard/layout.</summary>
public sealed record SaveLayoutRequest(IReadOnlyList<WidgetPlacementDto> Widgets);

// ---- Widget data DTOs ----------------------------------------------------------

/// <summary>A label/value pair used by count and distribution widgets.</summary>
public sealed record LabelValueDto(string Label, long Value);

/// <summary>RecentInventory rows.</summary>
public sealed record RecentInventoryItemDto(
    long Id,
    string DeviceId,
    string? ItemType,
    string Status,
    int? DeviceCount,
    DateTimeOffset SubmittedAt);

/// <summary>AssetsNearingEnd rows.</summary>
public sealed record AssetNearingEndDto(
    Guid Id,
    string Name,
    string? AssetTag,
    string? TypeName,
    DateOnly? WarrantyExpiry,
    int DaysRemaining);

/// <summary>AgentStatusSummary response.</summary>
public sealed record AgentStatusSummaryDto(
    long Active,
    long Suspended,
    long Deactivated,
    long OnlineLast5Minutes);

/// <summary>Uniform response envelope for GET /api/v1/dashboard/data/{widgetType}:
/// numeric widgets emit Values, list widgets emit Items. One shape keeps the frontend
/// widget host generic.</summary>
public sealed record WidgetDataDto(
    string WidgetType,
    IReadOnlyList<LabelValueDto>? Values,
    IReadOnlyList<object>? Items);
