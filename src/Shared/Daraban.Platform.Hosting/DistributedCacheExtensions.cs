using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Daraban.Platform.Hosting;

/// <summary>
/// Single registration point for the platform's <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
/// (Task 8.2). Before this existed there were three scattered <c>AddDistributedMemoryCache()</c>
/// calls (Host.Api, Host.AgentApi and -- the one that broke the "it's a one-line host change"
/// claim in the old comments -- the Settings module), so switching backends meant finding all
/// three. Everything now goes through <see cref="AddDarabanDistributedCache"/>.
///
/// Redis is used whenever a <c>ConnectionStrings:Redis</c> value is present, which is the case
/// in every deployed environment; with no connection string (local runs, unit tests) it falls
/// back to the in-process memory cache so no Redis is required to build or test. Modules never
/// see this class: they depend on <c>IDistributedCache</c> only, and the Redis client lives in
/// Shared rather than in any module.
/// </summary>
public static class DistributedCacheExtensions
{
    /// <summary>Configuration key holding the Redis connection string.</summary>
    public const string RedisConnectionStringName = "Redis";

    /// <summary>
    /// Redis keyspace prefix. Two apps sharing one Redis instance (Host.Api and Host.AgentApi
    /// do in production) must not collide, and a prefix keeps this platform's keys greppable
    /// and safely evictable.
    /// </summary>
    public const string RedisInstanceName = "daraban:";

    /// <summary>
    /// Registers the distributed cache. Idempotent: the first call wins and later calls are
    /// no-ops, so a host that also pulls in a module which registers its own cache does not
    /// end up with two competing <c>IDistributedCache</c> registrations.
    /// </summary>
    public static IServiceCollection AddDarabanDistributedCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache)))
            return services;

        var connectionString = configuration.GetConnectionString(RedisConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Local development and unit tests: an in-process cache implementing the same
            // interface, so behaviour is identical apart from not being shared between instances.
            services.AddDistributedMemoryCache();
            return services;
        }

        services.AddStackExchangeRedisCache(options =>
        {
            var redisOptions = ConfigurationOptions.Parse(connectionString);
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ClientName = "daraban";

            options.ConfigurationOptions = redisOptions;
            options.InstanceName = RedisInstanceName;
        });

        return services;
    }
}