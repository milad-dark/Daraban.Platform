using Daraban.Modules.Dashboard.Services.Dtos;
using Daraban.Modules.Dashboard.Services.Widgets;
using Daraban.Platform.Common;

namespace Daraban.Modules.Dashboard.Services.Interfaces;

/// <summary>Application service for the dashboard feature (Task 7.1).</summary>
public interface IDashboardService
{
    /// <summary>All available widget types with metadata (GET /dashboard/widgets).</summary>
    Result<IReadOnlyList<WidgetCatalog.WidgetDefinition>> GetWidgetCatalog();

    /// <summary>The user's saved layout, or the platform default when none is saved yet.</summary>
    Task<Result<DashboardLayoutDto>> GetLayoutAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Persists the user's layout (PUT /dashboard/layout).</summary>
    Task<Result<DashboardLayoutDto>> SaveLayoutAsync(
        Guid userId, SaveLayoutRequest request, CancellationToken ct = default);

    /// <summary>Runs the provider for a widget type (GET /dashboard/data/{widgetType}).</summary>
    Task<Result<WidgetDataDto>> GetWidgetDataAsync(WidgetType type, Guid entityId, CancellationToken ct = default);
}
