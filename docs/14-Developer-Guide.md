# Developer Guide — Daraban Platform (Task 8.5)

How to work in this repository: setup, the module recipe, permissions,
plugins, and tests. Conventions below are enforced by build or by test where
stated; the rest is enforced by code review. When in doubt, imitate the
Knowledge module — it is the most recent full vertical slice (entities →
repositories → services → controllers → tests → migration).

---

## 1. Project setup

**Prerequisites:** .NET 10 SDK (`dotnet --version` → 10.x), Node 22
(`frontend/`), Docker Engine + Compose plugin (for the stack and for
integration tests), Git. No local PostgreSQL/Redis/RabbitMQ needed — the
compose stack provides them.

```bash
git clone https://github.com/milad-dark/Daraban.Platform && cd Daraban.Platform
cp .env.example .env          # then set real passwords + DOCKERHUB_USERNAME
mkdir -p certs && openssl genrsa -out certs/jwt-signing-key.pem 3072 && chmod 600 certs/jwt-signing-key.pem
docker compose up -d          # postgres, redis, rabbitmq, hosts, workers, frontend, nginx
```

Backend (no Docker needed for day-to-day work):

```bash
dotnet build Daraban.Platform.sln
dotnet test Daraban.Platform.sln -c Release --filter "FullyQualifiedName!~Daraban.IntegrationTests"
```

Frontend:

```bash
cd frontend && npm ci --legacy-peer-deps && npx ng serve   # http://localhost:4200
```

First-run notes: the database schema is **not** bootstrapped automatically
(see `13-Database-Guide.md` §9 and `docs/09` §6.1 step 8). In Development the
hosts accept an ephemeral JWT key; in Production a missing
`Jwt__SigningKeyPemPath` is a startup crash by design.

---

## 2. Adding a new module (the recipe)

A module is three projects plus tests, wired in exactly five places. Skip a
step and the failure is silent (an unregistered service throws only when hit;
an unlisted controller 404s; an unwired project never compiles — the Software
module shipped all three failures at once in PR #23, fixed since).

**Step 1 — create the projects** (names are load-bearing; the DI, tests, and
docs all assume them):

```
src/Modules/<Name>/Daraban.Modules.<Name>.Api/          # controllers + AssemblyMarker.cs
src/Modules/<Name>/Daraban.Modules.<Name>.Services/     # services, DTOs, validators, DI extension
src/Modules/<Name>/Daraban.Modules.<Name>.Data/         # entities, Configurations/, repositories, DbContext
tests/UnitTests/Daraban.Modules.<Name>.Tests/          # xunit + Moq
```

The Api project uses `Sdk="Microsoft.NET.Sdk"` (not `Sdk.Web`) plus a
`FrameworkReference` to `Microsoft.AspNetCore.App`, and references Services +
`Daraban.Platform.Hosting` + `Daraban.Platform.Abstractions`. Copy any
existing `*.Api.csproj` — do not hand-write it (the Software Api shipped with
a two-levels-short relative path and `Sdk.Web`, and never compiled).

**Step 2 — the entity and its base type.** Pick the least powerful base that
fits: `BaseEntity` (audit columns only), `SoftDeletableEntity` (+
`is_deleted`), `TenantScopedEntity` (+ `entity_id`). New ids are
`Guid.CreateVersion7()` in the service — never `Guid.NewGuid()`, never
database-generated. Enums persist as strings (`HasConversion<string>`).
Cross-module references are bare `Guid`s with **no FK** (the KB precedent:
`KbTicketLink.TicketId`, `KbArticleTarget.TargetId`) — a foreign key across
schemas is a layering violation, not integrity.

**Step 3 — repository interface + implementation** in Data (`Add/Update` +
`SaveChangesAsync` on the interface, like every other module — the service
owns the transaction boundary, so `SaveChangesAsync` must be callable
separately from `AddAsync`). Name uniqueness checks take the
`(name, entityId, excludeId)` shape; compare the nullable-Guid pattern used
in `TicketTemplateRepository` (a bare `t.Id != excludeId` works only by
accident of SQL null semantics — be explicit).

**Step 4 — service returning `Result<T>`**, never throwing for expected
failures (validation, not-found, business rules). Validate paging on entry
(`NormalizePaging`: page ≥ 1, size clamped 1–200, default 20 — an unclamped
`page=0` produces `Skip(-pageSize)` and Postgres rejects it; every module
learned this separately). Write audit/history rows in the same
`SaveChangesAsync` as the mutation.

**Step 5 — wire the five places:**

1. `Daraban.Modules.<Name>.Services.<Name>ModuleServiceCollectionExtensions.Add<Name>Module`
   — DbContext (with `MigrationsHistoryTable("__EFMigrationsHistory", "<schema>")`),
   validators, repositories, services. One composition root per module (ADR-005).
2. Both hosts' `Program.cs`: `Add<Name>Module(...)` + `AddApplicationPart`
   (without the latter, controllers 404 — ASP.NET does not discover them).
3. `Daraban.Platform.sln` (+ solution folder + config + nesting entries), or
   `dotnet sln add`.
4. `Host.Api` references the Api + Data projects (AgentApi only if agents
   touch it).
5. `RequirePermission("<module>.<action>")` on every action (see §3).

**Step 6 — tests.** Empty `.csproj` test projects fail the build with "no
tests" noise; write real ones: service behavior with mocked repositories
(strict mocks — an unexpected call must fail, not return default), plus a
model test if the module has any non-trivial mapping (generated columns,
partial unique indexes, query filters). Mirror
`Daraban.Modules.Knowledge.Tests` for structure.

**Step 7 — migration + design-time factory.** Copy `KnowledgeDbContextFactory`
(an `IDesignTimeDbContextFactory` so migrations generate without booting a
host) and generate the module's initial migration (§9 of the Database Guide).

---

## 3. Adding a new permission

Permissions are open strings (`module.action`), resolved per request from
`UserProfileEntity` → `Profile` → `ProfileRight` and cached in Redis. Adding
one is three steps, and step 2 currently has no UI:

1. Put `[RequirePermission("<module>.<action>")]` on the action. Keep the
   existing granularity — read/write/delete, plus a distinct verb only when
   the act is materially different (`knowledge.publish`, `software.delete`).
   Publishing-gated transitions get their own permission, not a field flipped
   inside an edit DTO.
2. Insert the grant directly in the database (there is **no admin API or UI**
   for profiles/rights yet — Users/Auth/AuditLogs are the only Identity
   controllers):
   ```sql
   INSERT INTO identity.profile_rights (id, profile_id, module, action, is_recursive)
   VALUES (gen_random_uuid(), '<profile-id>', '<module>', '<action>', true);
   INSERT INTO identity.user_profile_entities (id, user_id, profile_id, entity_id, is_recursive, is_default)
   VALUES (gen_random_uuid(), '<user-id>', '<profile-id>', '<entity-id>', true, true);
   ```
3. Bust the permission cache for the affected user+entity
   (`IPermissionResolver.InvalidateAsync`) or wait out the 5-minute TTL.

Test it the way `PermissionEnforcementTests` does: same endpoint, granted
user gets 200, ungranted user gets 403 — never 404-vs-403 differences that
leak the permission's existence.

---

## 4. Writing a plugin

Plugins extend the platform after deployment without rebuilding the host
(ADR-007). The contract lives in `src/Shared/Daraban.Platform.Plugins`:

```csharp
public interface IPlugin
{
    string Id { get; }        // must match manifest id
    string Name { get; }
    string Version { get; }   // must match manifest version
    void ConfigureServices(IServiceCollection services);  // isolated collection
    void ConfigureApp(IApplicationBuilder app);           // endpoints/middleware
    IEnumerable<PluginMenuItem> GetMenuItems();           // Angular nav entries
}
```

Package layout (zip): the assembly + `manifest.json`
(`id`, `name`, `version`, `entryPoint`, `assemblyFile`, `author`,
`minimumHostVersion`, optional `remoteEntryUrl`) + SQL migrations.
Upload via `POST /api/v1/plugins` (admin only); enable/disable/uninstall via
the matching endpoints; each plugin gets its own `plugins_<id>.*` schema run
through `IPluginMigration`.

Rules that are load-bearing, not stylistic:

- Database access **only** through `IPluginDbContext` — the host
  `DbContext` and the connection string are never handed to plugin code.
- Assemblies load in an isolated collectible `AssemblyLoadContext`
  (`PluginLoadContext`); `Assembly.LoadFrom` pinning is how you leak memory
  on every reinstall.
- The upload path is zip-slip/zip-bomb surface: `PluginPackageManager`
  validates entry paths and sizes — do not bypass it with a second extractor.
- Menu items are data (`PluginMenuItemDto`), rendered by the shell; a plugin
  never injects Angular code into the host bundle.

---

## 5. Running tests

| Command | What runs | Notes |
|---|---|---|
| `dotnet test Daraban.Platform.sln -c Release --filter "FullyQualifiedName!~Daraban.IntegrationTests"` | All unit tests (1082 as of Task 8.5) | Mirrors CI exactly (`ci.yml`) |
| `dotnet test tests/UnitTests/Daraban.Modules.<X>.Tests` | One module | Fast feedback loop |
| `dotnet test tests/IntegrationTests/... --filter "FullyQualifiedName~Daraban.IntegrationTests"` | Testcontainers suite (Postgres+Redis+RabbitMQ) | Needs a Docker daemon; excluded from the unit stage |
| `docker build --target test -f docker/host-api.Dockerfile .` | Unit tests inside Docker | What CI actually executes |

Conventions that keep the suite honest: strict Moq behavior everywhere (an
unarranged call fails the test instead of returning default); no
`Thread.Sleep` (use the async primitives); time is injected or frozen, never
`DateTimeOffset.UtcNow` asserted exactly; Docker-dependent tests live only in
the IntegrationTests project, never in unit projects. Docs generators that
must stay truthful live beside the docs they feed: `docs/gen-endpoints.ps1`
(API inventory), `docs/gen-db.ps1` (schema inventory).
