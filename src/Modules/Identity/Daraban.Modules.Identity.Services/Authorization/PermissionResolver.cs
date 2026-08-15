using System.Text.Json;
using Daraban.Modules.Identity.Data;
using Daraban.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Daraban.Modules.Identity.Services.Authorization;

/// <summary>
/// Task 1.3 SS4.3's resolution algorithm, made real: union of ProfileRights across every
/// Profile the user holds in the target entity or in a recursive ancestor of it, cached so
/// this only hits the database once per user+entity per cache window instead of on every
/// single authorized request.
///
/// Caching backend is IDistributedCache -- registered as AddDistributedMemoryCache() for
/// now (Task 2.2/2.4), which is a free, zero-dependency, in-process implementation of the
/// exact same interface Redis would back. Swapping to AddStackExchangeRedisCache() later
/// (Task 1.3's original plan, once Redis is confirmed reliably available) is a one-line
/// Program.cs change -- nothing here needs to know or care which backend it's talking to.
/// </summary>
public class PermissionResolver : IPermissionResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private readonly IdentityDbContext _db;
    private readonly IEntityScopeAccessor _entityScope;
    private readonly IDistributedCache _cache;

    public PermissionResolver(IdentityDbContext db, IEntityScopeAccessor entityScope, IDistributedCache cache)
    {
        _db = db;
        _entityScope = entityScope;
        _cache = cache;
    }

    public async Task<IReadOnlySet<string>> ResolveAsync(Guid userId, Guid entityId, CancellationToken ct = default)
    {
        var cacheKey = CacheKey(userId, entityId);
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<HashSet<string>>(cached) ?? new HashSet<string>();

        var resolved = await ResolveFromDatabaseAsync(userId, entityId, ct);

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(resolved),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration }, ct);

        return resolved;
    }

    public Task InvalidateAsync(Guid userId, Guid entityId, CancellationToken ct = default)
        => _cache.RemoveAsync(CacheKey(userId, entityId), ct);

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
