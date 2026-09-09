using Daraban.Modules.Dashboard.Services.Dtos;

namespace Daraban.Modules.Dashboard.Services.Widgets;

/// <summary>
/// A widget data provider. One implementation per WidgetType -- the DI container resolves
/// them by the registered service key so the widget host stays open for extension (a future
/// plugin can register its own provider without touching this module).
/// </summary>
public interface IWidgetDataProvider
{
    /// <summary>The widget type this provider serves. Must match the DI registration key.</summary>
    WidgetType Type { get; }

    /// <summary>Fetches the widget payload. Implementations must be safe to call concurrently
    /// and must never throw for "no data" -- empty results are a valid payload.</summary>
    Task<WidgetDataDto> GetDataAsync(Guid entityId, CancellationToken ct);
}
