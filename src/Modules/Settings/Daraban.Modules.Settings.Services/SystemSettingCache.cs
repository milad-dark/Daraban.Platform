using System.Text.Json;
using Daraban.Modules.Settings.Data.Entities;
using Daraban.Modules.Settings.Data.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Daraban.Modules.Settings.Services;

/// <summary>
/// Two-layer cache over the settings table (Task 7.4): a Redis snapshot shared by every
/// host/worker instance plus a process-wide in-memory copy. Reads hit memory only --
/// configuration never adds per-request database load.
///
/// Registered as a SINGLETON: settings are process-wide constants between writes, which is
/// the entire point of the cache. The scoped repository is therefore never captured -- every
/// DB access opens its own DI scope via IServiceScopeFactory (the same shape ASP.NET Core
/// uses for IServiceProviderIsService-independent background work).
///
/// Consistency model: a write refreshes the Redis snapshot, so other instances pick up the
/// change on their next read (seconds, not minutes). While Redis is unavailable each
/// process keeps serving its last-known values from memory -- degraded freshness is
/// preferable to refusing requests because the configuration cache is down.
/// </summary>
public class SystemSettingCache(
    IServiceScopeFactory scopeFactory,
    IDistributedCache redis,
    ILogger<SystemSettingCache> logger)
{
    internal static readonly string RedisKey = "settings:snapshot";

    private static readonly JsonSerializerOptions SerializerOptions = new();

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private volatile SystemSetting[] _snapshot = [];

    /// <summary>All known settings. Populated by <see cref="EnsureLoadedAsync"/> at startup. Virtual so tests can substitute an in-memory snapshot.</summary>
    public virtual IReadOnlyList<SystemSetting> All => _snapshot;

    /// <summary>Single setting by exact key; null when unknown. Virtual for tests, like <see cref="All"/>.</summary>
    public virtual SystemSetting? Get(string key) =>
        Array.Find(_snapshot, s => s.Key == key);

    /// <summary>Loads settings from the database into memory and republishes to Redis. Virtual for tests.</summary>
    public virtual async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            var settings = await GetAllFromDbAsync(ct);
            _snapshot = settings;
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
            await PublishToRedisAsync(settings, ct);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<SystemSetting[]> GetAllFromDbAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISystemSettingRepository>();
        var settings = await repository.GetAllAsync(ct);
        return settings.ToArray();
    }

    /// <summary>Serializes and publishes the current table to Redis. Failures are logged,
    /// never thrown -- Redis being down must not fail a settings update.</summary>
    private async Task PublishToRedisAsync(IReadOnlyList<SystemSetting> settings, CancellationToken ct)
    {
        try
        {
            var payload = JsonSerializer.Serialize(
                settings.Select(ToSnapshot), SerializerOptions);
            await redis.SetStringAsync(RedisKey, payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Settings snapshot could not be written to Redis; this instance serves from local memory only.");
        }
    }

    internal sealed record SettingSnapshot(
        Guid Id, string Key, string Value, string ValueType, string Category,
        string Description, bool IsSecret, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    private static SettingSnapshot ToSnapshot(SystemSetting s) =>
        new(s.Id, s.Key, s.Value, s.ValueType, s.Category, s.Description, s.IsSecret, s.CreatedAt, s.UpdatedAt);
}
