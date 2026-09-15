using System.Text.Json;
using Daraban.Modules.Identity.Data;
using Daraban.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Daraban.Modules.Identity.Services.Authorization;

/// <summary>
/// Task 1.3 SS4.3's resolution algorithm, made real: union of ProfileRights across every
/// Profile the user holds in the target entity or in a recursive ancestor of it, cached so
/// this only hits the database once per user+entity per cache window instead of on every
/// single authorized request.
///
/// Caching backend is IDistributedCache -- Redis in every deployed environment (Task 8.2),
/// in-process memory when no Redis connection string is configured. Nothing here knows or
/// cares which backend it is talking to, so the same code path serves production, local runs
/// and unit tests.
///
/// The cache window is <see cref="PermissionCacheOptions.Ttl"/> (five minutes by default) with
/// jitter, and it is a *fallback* bound rather than the primary mechanism: every write path
/// that changes a user's effective rights calls <see cref="InvalidateAsync"/> so the change
/// lands immediately. Relying on expiry alone would mean a revoked right keeps working for up
/// to a full TTL, which on an authorization path is a security window, not a performance knob.
/// </summary>
public class PermissionResolver : IPermissionResolver
{
    private readonly IdentityDbContext _db;
    private readonly IEntityScopeAccessor _entityScope;
    private readonly IDistributedCache _cache;
    private readonly PermissionCacheOptions _options;

    public PermissionResolver(
        IdentityDbContext db,
        IEntityScopeAccessor entityScope,
        IDistributedCache cache,
        IOptions<PermissionCacheOptions>? options = null)
    {
        _db = db;
        _entityScope = entityScope;
        _cache = cache;
        // Optional so the many existing constructions (unit tests, others) keep compiling; the
        // fallback instance carries the same defaults the DI registration binds.
        _options = options?.Value ?? new PermissionCacheOptions();
    }

    public async Task<IReadOnlySet<string>> ResolveAsync(Guid userId, Guid entityId, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(userId, entityId);
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<HashSet<string>>(cached) ?? new HashSet<string>();

        var resolved = await ResolveFromDatabaseAsync(userId, entityId, ct);

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(resolved),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _options.NextLifetime() }, ct);

        return resolved;
    }

    public Task InvalidateAsync(Guid userId, Guid entityId, CancellationToken ct = default)
        => _cache.RemoveAsync(CacheKey(userId, entityId), ct);

        public async Task InvalidateUserAsync(Guid userId, CancellationToken ct = default)
        {
            // Enumerated from the grants themselves rather than guessed: an arbitrary entity id would
            // either miss the real entries or, worse, evict an unrelated tenant's.
            var grants = await _db.UserProfileEntities.AsNoTracking()
                .Where(upe => upe.UserId == userId)
                .Select(upe => new { upe.EntityId, upe.IsRecursive })
                .ToListAsync(ct);

            var entityIds = grants.Select(g => g.EntityId).ToHashSet();

            // A recursive grant caches under the *target* entity, not the entity the grant row names,
            // so evicting only `EntityId` would leave `perms:{user}:{descendant}` resident -- a revoked
            // right still answering from cache. Expanding the subtree here costs a few queries on a
            // path that only runs when an account is disabled or deleted, never on the request path.
            foreach (var grant in grants.Where(g => g.IsRecursive))
            {
                var scoped = await _entityScope.GetScopedEntityIdsAsync(grant.EntityId, recursive: true, ct);
                entityIds.UnionWith(scoped);
            }

            foreach (var entityId in entityIds)
                await _cache.RemoveAsync(CacheKey(userId, entityId), ct);
        }

    private async Task<HashSet<string>> ResolveFromDatabaseAsync(Guid userId, Guid entityId, CancellationToken ct)
    {
        var grants = await _db.UserProfileEntities.AsNoTracking()
            .Where(upe => upe.UserId == userId)
            .ToListAsync(ct);

        if (grants.Count == 0)
            return new HashSet<string>();

        // Direct grants (EntityId == the target entity) always count. Recursive grants
        // count if the target entity is that grant's entity itself or a descendant of it.
        var directEntityIds = grants.Where(g => g.EntityId == entityId).Select(g => g.ProfileId).ToHashSet();
        var recursiveGrants = grants.Where(g => g.IsRecursive && g.EntityId != entityId).ToList();

        var applicableProfileIds = new HashSet<Guid>(directEntityIds);
        if (recursiveGrants.Count > 0)
        {
            foreach (var grant in recursiveGrants)
            {
                var scoped = await _entityScope.GetScopedEntityIdsAsync(grant.EntityId, recursive: true, ct);
                if (scoped.Contains(entityId))
                    applicableProfileIds.Add(grant.ProfileId);
            }
        }

        if (applicableProfileIds.Count == 0)
            return new HashSet<string>();

        var rights = await _db.ProfileRights.AsNoTracking()
            .Where(r => applicableProfileIds.Contains(r.ProfileId))
            .Select(r => r.Module + "." + r.Action)
            .ToListAsync(ct);

        return rights.ToHashSet();
    }

    private static string CacheKey(Guid userId, Guid entityId) => $"perms:{userId}:{entityId}";
}
