using Daraban.Modules.Dashboard.Data.Repositories;
using Daraban.Modules.Dashboard.Services;
using Daraban.Modules.Dashboard.Services.Dtos;
using Daraban.Modules.Dashboard.Services.Widgets;
using FluentValidation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Daraban.Modules.Dashboard.Tests;

/// <summary>
/// Covers the Task 8.2 widget cache: hits, misses, expiry, the zero-TTL escape hatch, and the
/// deliberate no-caching of failures. Also proves the SizeLimit/Size contract the module's
/// MemoryCache registration relies on -- without Size set on every entry, the real cache throws.
/// </summary>
public class DashboardServiceWidgetCacheTests
{
    private const WidgetType Widget = WidgetType.OpenTicketsCount;
    private static readonly Guid EntityA = Guid.NewGuid();
    private static readonly Guid EntityB = Guid.NewGuid();

    private sealed class FakeWidgetProvider(WidgetType type, Func<Task<WidgetDataDto>> handler) : IWidgetDataProvider
    {
        public WidgetType Type { get; } = type;
        public int Calls { get; private set; }

        public Task<WidgetDataDto> GetDataAsync(Guid entityId, CancellationToken ct)
        {
            Calls++;
            return handler();
        }
    }

    private static (DashboardService Service, FakeWidgetProvider Provider, MemoryCacheOptions Options) CreateService(
        Func<Task<WidgetDataDto>>? handler = null,
        TimeSpan? cacheDuration = null,
        long? sizeLimit = 2_048)
    {
        handler ??= () => Task.FromResult(new WidgetDataDto(WidgetCatalog.ToApiName(Widget), [new LabelValueDto("Open", 1)], null));
        var provider = new FakeWidgetProvider(Widget, handler);
        var options = new WidgetOptions { CacheDuration = cacheDuration ?? TimeSpan.FromSeconds(30) };
        var cacheOptions = new MemoryCacheOptions { SizeLimit = sizeLimit };
        var cache = new MemoryCache(cacheOptions);
        var service = new DashboardService(
            Mock.Of<IDashboardLayoutRepository>(),
            Mock.Of<IValidator<SaveLayoutRequest>>(),
            [provider],
            options,
            cache,
            NullLogger<DashboardService>.Instance);
        return (service, provider, cacheOptions);
    }

    [Fact]
    public async Task SecondRequest_ServesFromCache_ProviderCalledOnce()
    {
        var (service, provider, _) = CreateService();

        var first = await service.GetWidgetDataAsync(Widget, EntityA);
        var second = await service.GetWidgetDataAsync(Widget, EntityA);

        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal(1, provider.Calls);
        Assert.Same(first.Value, second.Value);
    }

    [Fact]
    public async Task DifferentEntity_IsSeparateCacheEntry()
    {
        var (service, provider, _) = CreateService();

        await service.GetWidgetDataAsync(Widget, EntityA);
        await service.GetWidgetDataAsync(Widget, EntityB);

        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task ExpiredEntry_CallsProviderAgain()
    {
        var (service, provider, cacheOptions) = CreateService(cacheDuration: TimeSpan.FromMilliseconds(50));

        await service.GetWidgetDataAsync(Widget, EntityA);
        cacheOptions.ExpirationScanFrequency = TimeSpan.FromMilliseconds(1);
        await Task.Delay(150);
        await service.GetWidgetDataAsync(Widget, EntityA);

        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task ZeroCacheDuration_CachesNothing()
    {
        var (service, provider, _) = CreateService(cacheDuration: TimeSpan.Zero);

        await service.GetWidgetDataAsync(Widget, EntityA);
        await service.GetWidgetDataAsync(Widget, EntityA);

        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task FailingProvider_ReturnsEmptyWidget_AndIsNotCached()
    {
        var flip = false;
        var (service, provider, _) = CreateService(() =>
        {
            if (!flip)
            {
                flip = true;
                return Task.FromException<WidgetDataDto>(new InvalidOperationException("db down"));
            }
            return Task.FromResult(new WidgetDataDto(WidgetCatalog.ToApiName(Widget), [new LabelValueDto("Open", 7)], null));
        });

        var degraded = await service.GetWidgetDataAsync(Widget, EntityA);

        Assert.True(degraded.IsSuccess); // degrades, never fails the dashboard
        Assert.Empty(degraded.Value.Values!);
        Assert.Null(degraded.Value.Items);

        var recovered = await service.GetWidgetDataAsync(Widget, EntityA);
        Assert.Equal(7, recovered.Value.Values![0].Value);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task UnknownWidget_FailsBeforeCacheIsConsulted()
    {
        var (service, provider, _) = CreateService();

        // Valid enum member but no registered provider -- ToApiName would throw for
        // out-of-range values, which the controller's TryParse gate prevents upstream.
        var result = await service.GetWidgetDataAsync(WidgetType.AgentStatusSummary, EntityA);

        Assert.False(result.IsSuccess);
        Assert.Equal("DASHBOARD.WIDGET_NOT_FOUND", result.Error!.Code);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task SizeLimitContract_CachingIntoSizedCacheDoesNotThrow()
    {
        // MemoryCache with a SizeLimit throws unless every entry sets Size; this test exercises
        // the exact combination the module registers (SizeLimit=2048, Size=1 per entry).
        var (service, provider, _) = CreateService(sizeLimit: 2_048);

        var ids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();
        foreach (var id in ids)
        {
            var res = await service.GetWidgetDataAsync(Widget, id);
            Assert.True(res.IsSuccess);
        }

        Assert.Equal(ids.Count, provider.Calls); // all misses, no exceptions
    }

    [Fact]
    public void CacheKey_IsScopedToWidgetAndEntity()
    {
        var keyA = DashboardService.WidgetCacheKey(Widget, EntityA);
        var keyB = DashboardService.WidgetCacheKey(Widget, EntityB);
        var keyOtherWidget = DashboardService.WidgetCacheKey(WidgetType.TicketsByStatus, EntityA);

        Assert.NotEqual(keyA, keyB);
        Assert.NotEqual(keyA, keyOtherWidget);
        Assert.StartsWith("dashboard:widget:OpenTickets:", keyA);
    }
}
