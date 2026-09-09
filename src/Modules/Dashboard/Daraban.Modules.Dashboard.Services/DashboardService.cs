using Daraban.Modules.Dashboard.Data.Entities;
using Daraban.Modules.Dashboard.Data.Repositories;
using Daraban.Modules.Dashboard.Services.Dtos;
using Daraban.Modules.Dashboard.Services.Interfaces;
using Daraban.Modules.Dashboard.Services.Widgets;
using Daraban.Platform.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Daraban.Modules.Dashboard.Services;

/// <summary>
/// Dashboard feature implementation (Task 7.1). Layout persistence is per-user; widget data
/// comes from per-widget <see cref="IWidgetDataProvider"/> implementations resolved from DI.
/// All expected failures return <see cref="Result{T}"/> -- no exceptions for control flow.
/// </summary>
public sealed class DashboardService(
    IDashboardLayoutRepository layoutRepository,
    IValidator<SaveLayoutRequest> layoutValidator,
    IEnumerable<IWidgetDataProvider> widgetProviders,
    ILogger<DashboardService> logger) : IDashboardService
{
    private readonly Dictionary<string, IWidgetDataProvider> _providers =
        widgetProviders.ToDictionary(p => WidgetCatalog.ToApiName(p.Type), StringComparer.OrdinalIgnoreCase);

    public Result<IReadOnlyList<WidgetCatalog.WidgetDefinition>> GetWidgetCatalog() =>
        Result.Success<IReadOnlyList<WidgetCatalog.WidgetDefinition>>(WidgetCatalog.All);

    public async Task<Result<DashboardLayoutDto>> GetLayoutAsync(Guid userId, CancellationToken ct = default)
    {
        var layout = await layoutRepository.GetAsync(userId, "default", ct);
        if (layout is null)
        {
            // Fresh user: hand back an empty grid -- the frontend renders its default set
            // client-side and offers "Save this layout" to persist it.
            return Result.Success(new DashboardLayoutDto([], DateTimeOffset.UtcNow));
        }

        return Result.Success(new DashboardLayoutDto(
            layout.ParseLayout().Select(p => new WidgetPlacementDto(p.WidgetType, p.X, p.Y, p.W, p.H)).ToList(),
            layout.UpdatedAt));
    }

    public async Task<Result<DashboardLayoutDto>> SaveLayoutAsync(
        Guid userId, SaveLayoutRequest request, CancellationToken ct = default)
    {
        var validation = await layoutValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<DashboardLayoutDto>(
                new Error("DASHBOARD.LAYOUT_INVALID", message, ErrorType.Validation));
        }

        var placements = request.Widgets
            .Select(w => new WidgetPlacement { WidgetType = w.WidgetType, X = w.X, Y = w.Y, W = w.W, H = w.H })
            .ToList();

        var layout = new DashboardLayout
        {
            UserId = userId,
            Name = "default",
            LayoutJson = Serialize(placements),
            UpdatedById = userId,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        try
        {
            await layoutRepository.UpsertAsync(layout, ct);
            await layoutRepository.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
        {
            // Intentionally narrow: only persistence-layer failures are converted. Unexpected
            // bugs must still crash loudly rather than be swallowed as a "validation" error.
            logger.LogError(ex, "Failed to save dashboard layout for user {UserId}", userId);
            return Result.Failure<DashboardLayoutDto>(
                new Error("DASHBOARD.SAVE_FAILED", "The layout could not be saved.", ErrorType.BusinessRule));
        }

        return Result.Success(new DashboardLayoutDto(
            placements.Select(p => new WidgetPlacementDto(p.WidgetType, p.X, p.Y, p.W, p.H)).ToList(),
            layout.UpdatedAt));
    }

    public async Task<Result<WidgetDataDto>> GetWidgetDataAsync(WidgetType type, Guid entityId, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(WidgetCatalog.ToApiName(type), out var provider))
        {
            return Result.Failure<WidgetDataDto>(
                new Error("DASHBOARD.WIDGET_NOT_FOUND", $"No provider registered for widget '{type}'.", ErrorType.NotFound));
        }

        try
        {
            return Result.Success(await provider.GetDataAsync(entityId, ct));
        }
        catch (OperationCanceledException)
        {
            throw; // client disconnected -- normal cancellation, never masked
        }
        catch (Exception ex)
        {
            // Widget data is auxiliary: a broken provider must degrade to an empty widget,
            // not fail the whole dashboard. Logged at error for observability.
            logger.LogError(ex, "Widget data provider for {WidgetType} failed", type);
            return Result.Success(new WidgetDataDto(WidgetCatalog.ToApiName(type), [], null));
        }
    }

    private static string Serialize(List<WidgetPlacement> placements) =>
        System.Text.Json.JsonSerializer.Serialize(placements, DashboardLayout.JsonOptions);
}
