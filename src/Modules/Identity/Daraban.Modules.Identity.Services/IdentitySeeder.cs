using Daraban.Modules.Identity.Data;
using Daraban.Modules.Identity.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Daraban.Modules.Identity.Services;

/// <summary>
/// Seeds the baseline identity data a fresh database needs before the APIs are usable
/// (Task: default users + roles/permissions). Idempotent — every step is a no-op when
/// its rows already exist, so it runs on every Host.Api startup.
///
/// What it creates, only when missing:
///  1. <see cref="IdentitySeedCatalog.RootEntityName"/> — the tenant-tree root the two
///     seed profiles are granted in (a PermissionResolver grant always needs an entity).
///  2. <see cref="IdentitySeedCatalog.AdminProfileName"/> ("Super-Admin") — every right
///     in <see cref="IdentitySeedCatalog.AllPermissions"/>, the exact strings the
///     [RequirePermission(...)] attributes on the controllers check.
///  3. <see cref="IdentitySeedCatalog.UserProfileName"/> ("Standard User") — read-only
///     across the operational modules plus dashboard.write (own layout) — the working
///     set a non-admin needs to use the UI.
///  4. Two users (env-gated, Development only): <c>admin</c> with the Super-Admin
///     profile and <c>user</c> with the Standard profile.
///
/// Passwords: the two known usernames are reserved — AuthService.RegisterAsync rejects
/// them with IDENTITY.USERNAME_TAKEN, so they cannot be claimed by third parties before
/// the seed runs. Seeded passwords come from DARABAN_SEED_ADMIN_PASSWORD /
/// DARABAN_SEED_USER_PASSWORD and fall back to a single hardcoded default documented
/// below. Seeding never writes a plaintext password anywhere; PasswordHasher&lt;User&gt;
/// stores a PBKDF2 hash exactly like RegisterAsync does. Seeded accounts are marked
/// EmailConfirmed to keep the door open for confirmation-email enforcement later.
///
/// Bootstrap discipline: the seeded admin is for standing the system up, not for
/// living in. Operators should change both passwords (Users API or the env vars on a
/// re-run — UpdateSeedUserPasswordsAsync rehashes to match) and create real accounts.
/// </summary>
public class IdentitySeeder(
    IdentityDbContext db,
    IPasswordHasher<User> passwordHasher,
    IHostEnvironment environment,
    ILogger<IdentitySeeder> logger)
{
    public const string AdminPasswordEnvVar = "DARABAN_SEED_ADMIN_PASSWORD";
    public const string UserPasswordEnvVar = "DARABAN_SEED_USER_PASSWORD";

    // 16 chars, hits every rule in RegisterRequestValidator. Documented in the README;
    // change via the env vars above. The value only ever reaches PasswordHasher<T>.
    internal const string DefaultSeedPassword = "Chang3Me!LocalOnly";

    public virtual async Task SeedAsync(CancellationToken ct = default)
    {
        var rootEntity = await EnsureRootEntityAsync(ct);

        var adminProfile = await EnsureProfileWithRightsAsync(
            IdentitySeedCatalog.AdminProfileName,
            IdentitySeedCatalog.AllPermissions,
            ct);
        var userProfile = await EnsureProfileWithRightsAsync(
            IdentitySeedCatalog.UserProfileName,
            IdentitySeedCatalog.UserPermissions,
            ct);

        if (!environment.IsDevelopment())
        {
            logger.LogInformation(
                "Identity seed: profiles/entity ensured; default users skipped (environment '{Env}' is not Development)",
                environment.EnvironmentName);
            return;
        }

        await EnsureUserAsync(
            username: IdentitySeedCatalog.AdminUsername,
            email: "admin@daraban.local",
            displayName: "System Administrator",
            password: System.Environment.GetEnvironmentVariable(AdminPasswordEnvVar) ?? DefaultSeedPassword,
            profileId: adminProfile.Id,
            entityId: rootEntity.Id,
            ct);

        await EnsureUserAsync(
            username: IdentitySeedCatalog.UserUsername,
            email: "user@daraban.local",
            displayName: "Standard User",
            password: System.Environment.GetEnvironmentVariable(UserPasswordEnvVar) ?? DefaultSeedPassword,
            profileId: userProfile.Id,
            entityId: rootEntity.Id,
            ct);

        // EnsureUserAsync only sets the password at creation; this keeps an existing seed
        // account's hash in sync when the password env vars change between runs.
        await UpdateSeedUserPasswordsAsync(ct);

        logger.LogInformation(
            "Identity seed: default users ensured (admin, user). Passwords: {Source}",
            (System.Environment.GetEnvironmentVariable(AdminPasswordEnvVar), System.Environment.GetEnvironmentVariable(UserPasswordEnvVar)) switch
            {
                (null, null) => "built-in development default",
                (_, null) or (null, _) => "partly from environment variables",
                _ => "from environment variables"
            });
    }

    /// <summary>Rehashes the seed users' passwords to match the current environment
    /// variables (or the built-in default). Called after the rows-exist fast path so a
    /// password env var change takes effect on the next startup without manual SQL.</summary>
    public virtual async Task UpdateSeedUserPasswordsAsync(CancellationToken ct = default)
    {
        foreach (var (username, envVar) in new[]
                 {
                     (IdentitySeedCatalog.AdminUsername, AdminPasswordEnvVar),
                     (IdentitySeedCatalog.UserUsername, UserPasswordEnvVar),
                 })
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username, ct);
            if (user?.PasswordHash is null)
                continue;

            var password = System.Environment.GetEnvironmentVariable(envVar) ?? DefaultSeedPassword;
            var newHash = passwordHasher.HashPassword(user, password);
            if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password)
                == PasswordVerificationResult.Success)
                continue;

            user.PasswordHash = newHash;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            logger.LogInformation("Identity seed: rehashed password for seed user '{Username}'", username);
        }

        await db.SaveChangesAsync(ct);
    }

    // ---- helpers -----------------------------------------------------------

    /// <summary>The permission model needs exactly one entity to grant profiles in; the
    /// root of the tenant tree is that entity. Looked up by immutable FullName.</summary>
    private async Task<EntityNode> EnsureRootEntityAsync(CancellationToken ct)
    {
        var existing = await db.Entities.SingleOrDefaultAsync(
            e => e.FullPath == IdentitySeedCatalog.RootEntityName, ct);
        if (existing is not null)
            return existing;

        var entity = new EntityNode
        {
            Id = Guid.CreateVersion7(),
            Name = "Daraban",
            FullPath = IdentitySeedCatalog.RootEntityName,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Entities.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Identity seed: created root entity {Path}", entity.FullPath);
        return entity;
    }

    private async Task<Profile> EnsureProfileWithRightsAsync(string name, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.Name == name, ct);
        if (profile is null)
        {
            profile = new Profile { Id = Guid.CreateVersion7(), Name = name, IsDefault = false };
            db.Profiles.Add(profile);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Identity seed: created profile '{Name}'", name);
        }

        // Re-assert the right set every startup: new platform permissions are picked up
        // by the seeded profiles without any manual step. Existing rights keep their
        // identity (no delete/recreate), so nothing references them by row id.
        var existingRights = await db.ProfileRights
            .Where(r => r.ProfileId == profile.Id)
            .ToDictionaryAsync(r => (r.Module, r.Action), ct);

        foreach (var permission in permissions)
        {
            var separator = permission.IndexOf('.');
            var module = permission[..separator];
            var action = permission[(separator + 1)..];

            if (existingRights.ContainsKey((module, action)))
                continue;

            db.ProfileRights.Add(new ProfileRight
            {
                Id = Guid.CreateVersion7(),
                ProfileId = profile.Id,
                Module = module,
                Action = action,
                IsRecursive = true,
            });
        }

        await db.SaveChangesAsync(ct);
        return profile;
    }

    private async Task EnsureUserAsync(
        string username, string email, string displayName, string password,
        Guid profileId, Guid entityId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username, ct);
        if (user is null)
        {
            var now = DateTimeOffset.UtcNow;
            user = new User
            {
                Id = Guid.CreateVersion7(),
                Username = username,
                Email = email,
                DisplayName = displayName,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Identity seed: created user '{Username}'", username);
        }

        user.DefaultEntityId = entityId;
        await db.SaveChangesAsync(ct);

        var grant = await db.UserProfileEntities.SingleOrDefaultAsync(
            upe => upe.UserId == user.Id && upe.ProfileId == profileId && upe.EntityId == entityId, ct);
        if (grant is null)
        {
            db.UserProfileEntities.Add(new UserProfileEntity
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                ProfileId = profileId,
                EntityId = entityId,
                IsRecursive = true,
                IsDefault = true,
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Identity seed: granted '{Username}' their profile in the root entity", username);
        }
    }
}
