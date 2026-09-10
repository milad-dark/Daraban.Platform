using Daraban.Modules.Identity.Data.Entities;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daraban.Modules.Identity.Data.Auditing;

/// <summary>
/// Single owner of platform-wide change auditing (Task 7.3). Attached to
/// <see cref="IdentityDbContext"/> by <c>AddIdentityModule</c>; writes one immutable
/// <see cref="AuditLog"/> row per Added/Modified/Deleted entity inside the same SaveChanges
/// (and therefore the same transaction) as the change it describes.
///
/// Security: values of sensitive properties (password hashes, token hashes, client
/// secrets) are redacted before serialization -- see <see cref="SensitiveProperties"/> --
/// and whole entity types can be excluded via <c>Audit:ExcludedEntityTypes</c>.
/// Untracked bookkeeping columns (CreatedAt/UpdatedAt/...) are ignored so an audit row
/// describes the domain change, not the audit plumbing.
///
/// Request context: the actor is resolved from the HTTP request scope only -- non-HTTP
/// saves (workers, SignalR, design time) are system changes with no actor. IP and
/// user-agent come lazily from <see cref="IHttpContextAccessor"/> so the same interceptor
/// works on hosts and workers without per-host wiring; they are request metadata, not
/// credentials, so they are stored for forensics (truncated to the column cap).
/// </summary>
public sealed class AuditLogSaveChangesInterceptor(IHttpContextAccessor? httpContextAccessor, IConfiguration? configuration) : SaveChangesInterceptor
{
    /// <summary>camelCase JSON keys to match the platform's wire convention.</summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    /// <summary>Case-insensitive property names whose values must never be persisted.</summary>
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "TokenHash",
        "ClientSecretHash",
        "SecretHash",
        "Password",
        "ClientSecret",
        "ApiKey",
    };

    /// <summary>
    /// Property names whose values are foreign keys pointing at secrets (refresh tokens,
    /// agent credentials). The key itself is worthless on its own, but persisting it would
    /// let an audit reader correlate rows it cannot otherwise join -- e.g. which refresh
    /// token row belonged to which user. Replaced with "[REDACTED-FK]".
    /// </summary>
    private static readonly HashSet<string> SensitiveReferenceProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "UserId",       // RefreshTokens.UserId, UserProfileEntities.UserId
        "CredentialId", // AgentCredential references
        "ReplacedById", // RefreshTokens.ReplacedById (revocation chain)
        "OwnerUserId",
    };

    /// <summary>Tracked bookkeeping columns that add no forensic value in a diff.</summary>
    private static readonly HashSet<string> IgnoredProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(BaseEntity.UpdatedAt),
        nameof(BaseEntity.UpdatedById),
        nameof(BaseEntity.CreatedAt),
        nameof(BaseEntity.CreatedById),
    };

    private const int MaxUserAgentLength = 512;

    private readonly IHttpContextAccessor? _httpContextAccessor = httpContextAccessor;
    private readonly ISet<string> _excludedEntityTypes = (configuration?.GetSection("Audit:ExcludedEntityTypes").Get<string[]>() ?? [])
            .Select(t => t.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            CollectAuditEntries(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            CollectAuditEntries(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Runs inside SavingChanges (not SavedChanges) so the AuditLog rows join the same
    /// transaction: a rolled-back change must not leave an audit row behind. Added rows use
    /// a temporary negative PK so multiple rows in one SaveChanges never collide on
    /// Identity values.
    /// </summary>
    private void CollectAuditEntries(DbContext context)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var (actorId, ip, userAgent) = ResolveRequestContext();

        // Snapshot the tracked entries first: context.Add below mutates the ChangeTracker,
        // and Entries() enumerates it live -- adding during enumeration throws
        // InvalidOperationException ("Collection was modified").
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog
                || entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)
                || _excludedEntityTypes.Contains(GetAuditedEntityTypeName(entry)))
            {
                continue;
            }

            var (oldValues, newValues) = entry.State switch
            {
                EntityState.Added => (null, Serialize(entry, useCurrentValues: true)),
                EntityState.Deleted => (Serialize(entry, useCurrentValues: false), null),
                _ => (Serialize(entry, useCurrentValues: false), Serialize(entry, useCurrentValues: true)),
            };

            context.Add(new AuditLog
            {
                EntityType = GetAuditedEntityTypeName(entry),
                EntityId = ReadPrimaryKey(entry),
                Action = entry.State.ToString(),
                ActorUserId = actorId,
                OldValues = oldValues,
                NewValues = newValues,
                IpAddress = ip,
                UserAgent = userAgent,
                OccurredAt = occurredAt,
            });
        }
    }

    private static Guid ReadPrimaryKey(EntityEntry entry)
    {
        var keyProperty = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
        return keyProperty?.CurrentValue is Guid id ? id : Guid.Empty;
    }

    /// <summary>
    /// Some Npgsql/EF Core versions materialize entities into runtime types with a
    /// schema-decorated name (e.g. "identity.User"), so read EF's mapped type first and
    /// still trim a "schema." prefix defensively. The browser filters on bare names
    /// ("User", "Agent"), which is what this must always produce.
    /// </summary>
    private static string GetAuditedEntityTypeName(EntityEntry entry)
    {
        var name = entry.Metadata.ClrType.Name;
        var dot = name.IndexOf('.');
        return dot > 0 ? name[(dot + 1)..] : name;
    }

    private (Guid? ActorId, string? Ip, string? UserAgent) ResolveRequestContext()
    {
        // Only HTTP-driven changes can have an actor: resolve through the request scope, where
        // MSDI's GetService returns null -- never throws -- when a host does not register
        // ICurrentUser (e.g. Host.AgentApi). Everything else (workers, SignalR hubs, design
        // time) is a system change with no actor, which is exactly how the trail should read.
        // Never resolve via DbContext.GetService<ICurrentUser>: that API throws
        // InvalidOperationException instead of returning null when the service is missing.
        var http = _httpContextAccessor?.HttpContext;
        var requestServices = http?.RequestServices; // null outside a fully-pipeline request
        var currentUser = requestServices?.GetService<ICurrentUser>();
        var actor = currentUser is { IsAuthenticated: true } ? currentUser.UserId : (Guid?)null;

        var ip = http?.Connection.RemoteIpAddress?.ToString();
        var userAgent = http?.Request.Headers.UserAgent.ToString();

        // Column is capped at 512 -- truncate instead of throwing on an oversized header.
        if (userAgent is { Length: > MaxUserAgentLength })
        {
            userAgent = userAgent[..MaxUserAgentLength];
        }

        return (actor, ip, userAgent);
    }

    private static string? Serialize(EntityEntry entry, bool useCurrentValues)
    {
        var node = new JsonObject();

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey() || property.Metadata.IsShadowProperty())
            {
                continue;
            }

            // Keys are camelCased here rather than via DictionaryKeyPolicy: JsonObject members
            // set through the indexer are pre-built nodes and never pass through the naming
            // policy. Values (nested objects, enums) go through the serializer untouched.
            var key = JsonNamingPolicy.CamelCase.ConvertName(property.Metadata.Name);

            if (SensitiveProperties.Contains(property.Metadata.Name))
            {
                node[key] = "[REDACTED]";
                continue;
            }

            if (SensitiveReferenceProperties.Contains(property.Metadata.Name))
            {
                node[key] = "[REDACTED-FK]";
                continue;
            }

            if (IgnoredProperties.Contains(property.Metadata.Name))
            {
                continue;
            }

            // Deleted/Modified entries keep their original values; Added entries have none.
            var value = useCurrentValues ? property.CurrentValue : property.OriginalValue;

            node[key] = value is null
                ? null
                : JsonSerializer.SerializeToNode(value, SerializerOptions);
        }

        return node.Count == 0 ? null : node.ToJsonString(SerializerOptions);
    }
}
