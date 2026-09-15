using System.Text;
using Daraban.Modules.Settings.Data.Entities;
using Daraban.Modules.Settings.Data.Repositories;
using Daraban.Modules.Settings.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daraban.Modules.Settings.Tests;

/// <summary>
/// SystemSettingCache (Task 8.2): the freshness window that keeps settings reads off the
/// database, the Redis revalidation path that lets a peer instance observe a write, and the
/// refusal of snapshots published by a different catalog version. Uses a recording
/// IDistributedCache and a counting repository so "did this touch the database" is observable.
/// </summary>
public class SystemSettingCacheTests
{
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<ISystemSettingRepository> _repository = new();
    private readonly RecordingCache _redis = new();
    private readonly ServiceProvider _provider;

    public SystemSettingCacheTests()
    {
            Rows =
            [
                Row("branding.application_name", "Daraban Platform"),
                Row("email.smtp_host", "smtp.example.com"),
            ];

        _repository
                .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Rows);
            _repository
                .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, CancellationToken _) => Rows.FirstOrDefault(s => s.Key == key));
            _repository
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var services = new ServiceCollection();
            services.AddSingleton(_repository.Object);
            _provider = services.BuildServiceProvider();
    }

        private readonly List<SystemSetting> Rows;

    private int DbReads { get; set; }

    private SystemSettingCache CreateSut(TimeSpan? freshnessWindow = null) =>
        new(_provider.GetRequiredService<IServiceScopeFactory>(), _redis,
            Mock.Of<ILogger<SystemSettingCache>>(),
            Options.Create(new SystemSettingCacheOptions
            {
                FreshnessWindow = freshnessWindow ?? TimeSpan.FromSeconds(5),
            }));

    private static SystemSetting Row(string key, string value) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Value = value,
        ValueType = "string",
        Category = key.Split('.')[0],
        Description = key,
        IsSecret = false,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    // ---- Freshness window ------------------------------------------------------------------

    [Fact]
    public async Task EnsureLoaded_Reads_The_Database_Only_Once_Inside_The_Freshness_Window()
    {
        var sut = CreateSut();
        TrackDbReads();

        await sut.EnsureLoadedAsync();
        await sut.EnsureLoadedAsync();
        await sut.EnsureLoadedAsync();

        Assert.Equal(1, DbReads);
        Assert.Equal(2, sut.All.Count);
    }

    [Fact]
        public async Task EnsureLoaded_Revalidates_Against_Redis_Once_The_Freshness_Window_Has_Elapsed()
    {
        // Zero means "consult Redis on every read", which is the documented way to make the
        // window elapse deterministically in a unit test.
        var sut = CreateSut(TimeSpan.Zero);
        TrackDbReads();

        await sut.EnsureLoadedAsync();
        await sut.EnsureLoadedAsync();

            // The second read revalidated (Redis was consulted again) and adopted the snapshot it
            // found there, so the database stayed untouched after the initial cold load.
            Assert.Equal(2, _redis.Reads);
            Assert.Equal(1, DbReads);
        }

        [Fact]
        public async Task EnsureLoaded_Honours_A_Write_Made_Through_UpdateAsync_Without_Re_Reading()
        {
            var sut = CreateSut();
            TrackDbReads();

        await sut.EnsureLoadedAsync();
            await sut.UpdateAsync("branding.application_name", "Renamed", ActorId);
            var readsAfterUpdate = DbReads;

            await sut.EnsureLoadedAsync();

            Assert.Equal(readsAfterUpdate, DbReads);
            Assert.Equal("Renamed", sut.Get("branding.application_name")!.Value);
        }

    // ---- Cross-instance propagation --------------------------------------------------------

    [Fact]
    public async Task EnsureLoaded_Adopts_A_Peer_Snapshot_Without_Touching_The_Database()
    {
        // Instance one warms the shared Redis entry from the database...
        var writer = CreateSut();
        TrackDbReads();
        await writer.EnsureLoadedAsync();

        // ...instance two starts cold and must serve from that snapshot alone.
        var reader = new SystemSettingCache(
            _provider.GetRequiredService<IServiceScopeFactory>(), _redis,
            Mock.Of<ILogger<SystemSettingCache>>(),
            Options.Create(new SystemSettingCacheOptions { FreshnessWindow = TimeSpan.Zero }));

        await reader.EnsureLoadedAsync();

        Assert.Equal(1, DbReads);
        Assert.Equal(2, reader.All.Count);
        Assert.Equal("smtp.example.com", reader.Get("email.smtp_host")!.Value);
    }

    [Fact]
    public async Task EnsureLoaded_Propagates_A_Write_To_A_Peer_Once_The_Window_Elapses()
    {
        var writer = CreateSut();
        await writer.EnsureLoadedAsync();
        await writer.UpdateAsync("email.smtp_host", "smtp.changed.example.com", ActorId);

        var reader = new SystemSettingCache(
            _provider.GetRequiredService<IServiceScopeFactory>(), _redis,
            Mock.Of<ILogger<SystemSettingCache>>(),
            Options.Create(new SystemSettingCacheOptions { FreshnessWindow = TimeSpan.Zero }));

        await reader.EnsureLoadedAsync();

        Assert.Equal("smtp.changed.example.com", reader.Get("email.smtp_host")!.Value);
    }

    // ---- Version guard and resilience ------------------------------------------------------

    [Fact]
    public async Task EnsureLoaded_Refuses_A_Snapshot_From_A_Different_Catalog_Version()
    {
        _redis.Seed(_redis.LastKey ?? "settings:snapshot",
            """{"ProcessVersion":"0000000000000000000000000000000000000000000000000000000000000000","Settings":[]}""");

        var sut = CreateSut();
        TrackDbReads();

        await sut.EnsureLoadedAsync();

        Assert.Equal(1, DbReads);
        Assert.Equal(2, sut.All.Count);
    }

    [Fact]
    public async Task EnsureLoaded_Falls_Back_To_The_Database_When_Redis_Throws()
    {
        _redis.ThrowOnAccess = true;

        var sut = CreateSut();

        // Must not throw: an unreachable cache degrades freshness, it does not fail the request.
        await sut.EnsureLoadedAsync();

        Assert.Equal(2, sut.All.Count);
    }

    [Fact]
    public async Task EnsureLoaded_Falls_Back_To_The_Database_When_The_Snapshot_Is_Unreadable()
    {
        _redis.Seed("settings:snapshot", "not-json");

        var sut = CreateSut();

        await sut.EnsureLoadedAsync();

        Assert.Equal(2, sut.All.Count);
    }

    [Fact]
    public async Task UpdateAsync_Still_Commits_When_Redis_Is_Unavailable()
    {
        _redis.ThrowOnAccess = true;
        var sut = CreateSut();

        await sut.UpdateAsync("email.smtp_host", "smtp.changed.example.com", ActorId);

        Assert.Equal("smtp.changed.example.com", sut.Get("email.smtp_host")!.Value);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private void TrackDbReads() =>
        _repository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .Callback(() => DbReads++)
            .ReturnsAsync(() => Rows);

    /// <summary>IDistributedCache stand-in that records traffic, can be pre-seeded with a peer's
    /// payload, and can be made to fail so the resilience paths are reachable.</summary>
    private sealed class RecordingCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _entries = new(StringComparer.Ordinal);

        public int Reads { get; private set; }

        public bool ThrowOnAccess { get; set; }

        public string? LastKey { get; private set; }

        public void Seed(string key, string value) =>
            _entries[key] = Encoding.UTF8.GetBytes(value);

        public byte[]? Get(string key)
        {
            Reads++;
            LastKey = key;
            FailIfConfigured();
            return _entries.TryGetValue(key, out var value) ? value : null;
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            LastKey = key;
            FailIfConfigured();
            _entries[key] = value;
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key) { }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key) => _entries.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        private void FailIfConfigured()
        {
            if (ThrowOnAccess)
            {
                throw new InvalidOperationException("Redis is unreachable (simulated).");
            }
        }
    }
}