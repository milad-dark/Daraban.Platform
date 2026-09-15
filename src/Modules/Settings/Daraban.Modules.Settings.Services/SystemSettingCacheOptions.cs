namespace Daraban.Modules.Settings.Services;

/// <summary>
/// Tunable knobs for the system-settings cache (Task 8.2). Bound from the
/// <c>"Settings"</c> configuration section.
/// </summary>
public sealed class SystemSettingCacheOptions
{
    public const string SectionName = "Settings";

    /// <summary>
    /// How long this instance trusts its local snapshot before consulting Redis for a version
    /// published by a peer. This is the platform's settings-propagation delay across instances:
    /// an admin saving a value sees it immediately (the write updates local memory), but a peer
    /// sees it after at most this window. Zero means "consult Redis on every read".
    /// </summary>
    /// <remarks>
    /// The window exists so a busy process does not pay a Redis round-trip per settings read.
    /// Reads never touch the database again after startup, so raising this costs propagation
    /// latency and nothing else.
    /// </remarks>
    public TimeSpan FreshnessWindow { get; set; } = TimeSpan.FromSeconds(5);
}