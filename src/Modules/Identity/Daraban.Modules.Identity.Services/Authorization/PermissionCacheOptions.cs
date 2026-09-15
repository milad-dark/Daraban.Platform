namespace Daraban.Modules.Identity.Services.Authorization;

/// <summary>
/// Tunable knobs for the permission-resolution cache (Task 8.2). Bound from the
/// <c>"PermissionCache"</c> configuration section; the defaults are what the platform shipped
/// with before this existed (a flat five minutes), so an environment that configures nothing
/// sees the same behaviour.
/// </summary>
public sealed class PermissionCacheOptions
{
    public const string SectionName = "PermissionCache";

    /// <summary>
    /// How long a resolved permission set is cached. This is the platform's effective
    /// authorization-propagation delay: revoking a right takes up to this long to take effect on
    /// instances that hold a cached entry, unless the write path explicitly invalidates it (which
    /// <see cref="PermissionResolver.InvalidateAsync"/> is for). Raising it trades correctness for
    /// fewer database round-trips on the hottest path in the app -- every authorized request.
    /// </summary>
    public TimeSpan Ttl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Random addition (0..Jitter) applied to each entry's lifetime. Without it, a burst of
    /// requests that all miss at once -- a deploy, or a cache flush -- produce entries that all
    /// expire on the same second and stampede the database together. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan Jitter { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Minimum allowed <see cref="Ttl"/>. Guards against an operator setting the TTL to zero (or
    /// negative), which would silently turn every authorized request into a database round-trip
    /// and make the cache look "broken" rather than "disabled".
    /// </summary>
    public static readonly TimeSpan MinimumTtl = TimeSpan.FromSeconds(5);

    /// <summary>The effective, validated lifetime for one cache entry, jitter included.</summary>
    public TimeSpan NextLifetime()
    {
        var ttl = Ttl < MinimumTtl ? MinimumTtl : Ttl;
        if (Jitter <= TimeSpan.Zero)
            return ttl;

        // Random.Shared is thread-safe and needs no lock; this is not security-sensitive.
        var jitterMs = Random.Shared.NextInt64(0, (long)Jitter.TotalMilliseconds + 1);
        return ttl + TimeSpan.FromMilliseconds(jitterMs);
    }
}