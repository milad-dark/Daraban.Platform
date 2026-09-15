namespace Daraban.Modules.Dashboard.Services.Widgets;

/// <summary>
/// Tunable widget behavior, bound to the "Dashboard" configuration section. Kept as an
/// interface so the values are injectable and unit-testable without configuration.
/// </summary>
public interface IWidgetOptions
{
    /// <summary>Warranty-expiry warning horizon in days (AssetsNearingEnd widget).</summary>
    int WarrantyWarningDays { get; }

    /// <summary>Rolling window in days for SLA compliance (SlaComplianceRate widget).</summary>
    int SlaWindowDays { get; }

    /// <summary>
    /// How long a widget payload stays cached, per entity. Bound from
    /// "Dashboard:CacheDuration". The widget data is read-only aggregation, so a short cache
    /// removes the per-render query burst on a dashboard with eight widgets without the user
    /// noticing the staleness. Zero disables widget caching entirely.
    /// </summary>
    TimeSpan CacheDuration { get; }
}

/// <summary>Default binding from configuration "Dashboard:WarrantyWarningDays" etc.</summary>
public sealed class WidgetOptions : IWidgetOptions
{
    public int WarrantyWarningDays { get; init; } = 90;
    public int SlaWindowDays { get; init; } = 30;
    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromSeconds(30);
}
