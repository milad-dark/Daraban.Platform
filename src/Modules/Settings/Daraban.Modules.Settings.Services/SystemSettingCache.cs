using System.Text.Json;
using Daraban.Modules.Settings.Data.Entities;
using Daraban.Modules.Settings.Data.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daraban.Modules.Settings.Services;

/// <summary>
/// Two-layer cache over the settings table (Task 7.4, reworked in Task 8.2): a Redis snapshot
/// shared by every host/worker instance plus a process-wide in-memory copy. Reads are served
/// from memory and never touch the database after the first load.
///
/// Registered as a SINGLETON: settings are process-wide constants between writes, which is the
/// entire point of the cache. The scoped repository is therefore never captured -- every DB
/// access opens its own DI scope via IServiceScopeFactory (the same shape ASP.NET Core uses for
/// IServiceProviderIsService-independent background work).
///
/// Consistency model: the snapshot is BOTH written to and read from Redis, and it carries the
/// catalog fingerprint of the process that wrote it. Each instance revalidates against Redis
/// once its local copy is older than <see cref="SystemSettingCacheOptions.FreshnessWindow"/>
/// (five seconds by default), so a value saved on one instance reaches its peers within that
/// window rather than never. The writer itself is immediately consistent -- the write updates
/// local memory before returning. A snapshot written by a different catalog version is refused,
/// so a rolling deploy cannot make an instance serve keys its own catalog does not define.
///
/// While Redis is unavailable each process keeps serving its last-known values from memory:
/// degraded freshness is preferable to refusing requests because the configuration cache is
/// down. Every Redis failure is logged and swallowed -- a settings update must never fail
/// because a cache is unreachable.
/// </summary>
public class SystemSettingCache(
    IServiceScopeFactory scopeFactory,
    IDistributedCache redis,
    ILogger<SystemSettingCache> logger,
    IOptions<SystemSettingCacheOptions>? options = null)
{
    internal static readonly string RedisKey = "settings:snapshot";

    private static readonly JsonSerializerOptions SerializerOptions = new();

    /// <summary>Fingerprint of the catalog definition set in THIS process. Stamped into the
    /// published snapshot and required on read, so an instance never adopts settings shaped by a
    /// different build.</summary>
    private static readonly string CatalogFingerprint = SettingsSeeder.ComputeFingerprint();

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly TimeSpan _freshnessWindow =
        (options ?? Options.Create(new SystemSettingCacheOptions())).Value.FreshnessWindow;

    /// <summary>Tick at which the snapshot was last confirmed current. TickCount64 is an unsigned
    /// monotonic counter, so this is immune to system clock changes. long.MinValue means "never".</summary>
    private long _snapshotConfirmedAtTick = long.MinValue;

    private volatile SystemSetting[] _snapshot = [];

    /// <summary>All known settings. Populated by <see cref="EnsureLoadedAsync"/> at startup. Virtual so tests can substitute an in-memory snapshot.</summary>
    public virtual IReadOnlyList<SystemSetting> All => _snapshot;

    /// <summary>Single setting by exact key; null when unknown. Virtual for tests, like <see cref="All"/>.</summary>
    public virtual SystemSetting? Get(string key) =>
        Array.Find(_snapshot, s => s.Key == key);

    /// <summary>
        /// Makes the snapshot current: a no-op while the local copy is still within the freshness
        /// window, otherwise a Redis revalidation, and failing that a full reload from the database.
        /// Virtual for tests.
        /// </summary>
        public virtual async Task EnsureLoadedAsync(CancellationToken ct = default)
        {
            if (IsSnapshotCurrent())
            {
                return;
            }

            await _refreshLock.WaitAsync(ct);
            try
            {
                // Re-check under the lock: several callers can queue up behind one refresh.
                if (IsSnapshotCurrent())
                {
                    return;
                }

                var published = await TryReadFromRedisAsync(ct);
                if (published is { Length: > 0 })
                {
                    _snapshot = published;
                    ConfirmSnapshot();
                    return;
                }

                var settings = await GetAllFromDbAsync(ct);
                _snapshot = settings;
                ConfirmSnapshot();
                await PublishToRedisAsync(settings, ct);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

    /// <summary>
    /// Persists a new value and refreshes both cache layers. Runs in its own scope because
    /// the cache is a singleton -- it must never borrow a request's DbContext. Virtual for tests.
    /// </summary>
    public virtual async Task UpdateAsync(
        string key, string newValue, Guid actorId, CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ISystemSettingRepository>();

            var setting = await repository.GetByKeyAsync(key, ct);
            if (setting is null)
            {
                throw new InvalidOperationException($"Setting '{key}' vanished between cache read and write -- re-run the seeder.");
            }

            setting.Value = newValue;
            setting.UpdatedAt = DateTimeOffset.UtcNow;
            setting.UpdatedById = actorId;

            await repository.SaveChangesAsync(ct);

            var settings = await repository.GetAllAsync(ct);
            _snapshot = settings.ToArray();
                        // The writer is immediately consistent, and this also stops the next read from
                        // re-reading the value it just published.
                        ConfirmSnapshot();
                        await PublishToRedisAsync(settings, ct);
                    }
                    finally
                    {
                        _refreshLock.Release();
                    }
                }

                private bool IsSnapshotCurrent()
                {
                    var confirmedAt = Interlocked.Read(ref _snapshotConfirmedAtTick);
                    if (confirmedAt == long.MinValue)
                    {
                        return false;
                    }

                    return Environment.TickCount64 - confirmedAt < (long)_freshnessWindow.TotalMilliseconds;
                }

                private void ConfirmSnapshot() =>
                    Interlocked.Exchange(ref _snapshotConfirmedAtTick, Environment.TickCount64);

                private async Task<SystemSetting[]> GetAllFromDbAsync(CancellationToken ct)
                {
                    using var scope = scopeFactory.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<ISystemSettingRepository>();
                    var settings = await repository.GetAllAsync(ct);
                    return settings.ToArray();
    }

                /// <summary>Reads the peer-published snapshot. Returns null -- never throws -- when Redis is
                /// unreachable, holds nothing, or holds a snapshot from a different catalog version.</summary>
                private async Task<SystemSetting[]?> TryReadFromRedisAsync(CancellationToken ct)
                {
                    try
                    {
                        var payload = await redis.GetStringAsync(RedisKey, ct);
                        if (string.IsNullOrEmpty(payload))
                        {
                            return null;
                        }

                        var published = JsonSerializer.Deserialize<PublishedSnapshot>(payload, SerializerOptions);
                        if (published is null || !string.Equals(published.ProcessVersion, CatalogFingerprint, StringComparison.Ordinal))
                        {
                            logger.LogWarning(
                                "Discarded the settings snapshot in Redis: it was published by a different catalog version.");
                            return null;
                        }

                        return published.Settings.Select(ToEntity).ToArray();
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "Settings snapshot could not be read from Redis; falling back to the database.");
                        return null;
                    }
                }

                /// <summary>Serializes and publishes the current table to Redis. Failures are logged,
                /// never thrown -- Redis being down must not fail a settings update.</summary>
                private async Task PublishToRedisAsync(IReadOnlyList<SystemSetting> settings, CancellationToken ct)
                {
                    try
                    {
                        var payload = JsonSerializer.Serialize(
                            new PublishedSnapshot(CatalogFingerprint, settings.Select(ToSnapshot).ToArray()),
                            SerializerOptions);
                        await redis.SetStringAsync(RedisKey, payload,
                            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) }, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "Settings snapshot could not be written to Redis; this instance serves from local memory only.");
                    }
                }

                internal sealed record PublishedSnapshot(string ProcessVersion, SettingSnapshot[] Settings);

                internal sealed record SettingSnapshot(
                    Guid Id, string Key, string Value, string ValueType, string Category,
                    string Description, bool IsSecret, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

                private static SettingSnapshot ToSnapshot(SystemSetting s) =>
                    new(s.Id, s.Key, s.Value, s.ValueType, s.Category, s.Description, s.IsSecret, s.CreatedAt, s.UpdatedAt);

                private static SystemSetting ToEntity(SettingSnapshot s) => new()
                {
                    Id = s.Id,
                    Key = s.Key,
                    Value = s.Value,
                    ValueType = s.ValueType,
                    Category = s.Category,
                    Description = s.Description,
                    IsSecret = s.IsSecret,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt,
                };
            }
