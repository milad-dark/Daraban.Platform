# ADR-007: Plugin framework via isolated AssemblyLoadContext

## Status
Accepted

## Date
2026-09-11

## Context
Daraban must let third parties extend the platform after deployment — new asset
categories, ticket automation rules, report types, and integrations (Slack, Teams,
LDAP, ...) — without rebuilding or redeploying the host. The platform is a modular
monolith (ADR-001), so the extension mechanism must not break module boundaries or
the single-deployment story, yet plugins are *not* trusted platform code: they are
uploaded at runtime by an administrator as a `.zip` package (DLL + `manifest.json` +
SQL migrations), potentially by a vendor the platform has never seen at build time.

Requirements that shaped the decision:

1. **Isolation**: a plugin's types must not leak into the host's type resolution, and
   unloading must actually release the assembly (no `Assembly.LoadFrom` pinning).
2. **Least privilege**: plugins must not reach the host's `IServiceProvider`, its
   connection string, or any schema but their own.
3. **Safety of the upload path itself**: zip extraction is the classic zip-slip /
   zip-bomb attack surface; the server must reject path traversal, oversized
   packages, and decompression bombs before touching disk.
4. **Auditability**: install/uninstall must leave a durable registry row even after
   the plugin's files and schema are gone.
5. **DB schema per plugin**: plugin tables live in `plugins_{id}` schemas, keeping
   plugin data out of core schemas and making uninstall a single `DROP SCHEMA`.

## Decision

### Runtime: collectible AssemblyLoadContext
Plugins load through a custom `PluginLoadContext(pluginId, assemblyPath)` that derives
from .NET's collectible `AssemblyLoadContext`. It resolves the plugin's own assembly and
its dependencies from the plugin directory, but **falls back to the default ALC for
`Daraban.Platform.Plugins` (the contract assembly) and the host's shared assemblies**
so a plugin can never substitute its own copy of a contract type — the host always
compares plugin objects against the one contract assembly it trusts. Unloading the
context (`Unload()` + GC reachability) releases the assembly for real, which
`PluginManager.UninstallAsync` relies on when deleting plugin files on Windows.

### Package pipeline: validate everything, then extract
`PluginPackageManager.ExtractPackageAsync` enforces, in order: non-empty stream,
`MaxPackageSizeBytes` (20 MB), `MaxPackageEntries` (200), manifest presence and
`PluginManifest.Validate` (id pattern, semver, entry point, assembly file, plugin
type, http(s)-only `remoteEntryUrl`), total uncompressed cap (`MaxUncompressedBytes`,
100 MB — the zip-bomb guard), then extracts via `GetSafeEntryPath` which rejects
`..`/`.` components, any `:` (drive letters + NTFS alternate data streams), rooted
names, and trailing space/dot segments before a single `Path.Combine`. Extraction
targets `/plugins/{pluginId}/` — an existing directory means a conflicting prior
install and aborts the operation.

### Registry and lifecycle (PluginService + IPluginManager)
A `core.plugins` row tracks each install (`id, name, version, status, installed_at,
manifest_json`, ADR-001 conventions). The state machine is strict:

```
              install            enable           disable
  (none) ─────────────► enabled ─────────────► enabled/disabled
                          ▲   │                    │
                          │   │ uninstall         │ uninstall
                          │   ▼                    ▼
             enabled/disabled ─────────► uninstalled (tombstone)
```

- **Install** while a row exists with `Status != "uninstalled"` → `PLUGINS.INVALID_STATE`
  conflict; reinstalling over a tombstone is allowed (fresh row content).
- **Enable/Disable/Uninstall** on an uninstalled (tombstone) row → `PLUGINS.INVALID_STATE`;
  tombstones are never resurrected as "disabled" — they can only be replaced by a new
  install.
- Uninstall keeps the tombstone for audit while dropping the plugin's schema and
  deleting its files.

### Database: one schema per plugin, quoted at every call site
`PluginMigrationRunner.GetSchemaName(pluginId)` maps `plugins_{id}` with `-` → `_`.
The plugin id arrives pre-validated by the manifest's `^[a-z0-9][a-z0-9-]{1,62}[a-z0-9]$`
pattern, and — defense in depth — every SQL call site double-quotes the schema
(`CREATE SCHEMA IF NOT EXISTS "{schema}"`). Migrations run inside per-migration
transactions with a per-plugin `__plugin_migrations` history table. Plugin data access
goes through a pre-registered `IPluginDbContext` whose Npgsql connection carries
`SearchPath = plugins_{id}` — plugins never see a connection string and cannot
address another plugin's or a core schema.

### Service registration: restricted scope
`IPlugin.ConfigureServices(IServiceCollection)` runs against a **separate**
`ServiceCollection` the host owns; the plugin can register its own services but never
receives the host's container. `IPluginDbContext` is pre-registered into that
collection by the host. `IPlugin.ConfigureApp` is **intentionally never invoked** —
the host pipeline is owned by the composition root (ADR-005); a plugin hooking
`IApplicationBuilder` would violate the "hosts own the pipeline" rule. The method
remains on the interface for future host-mediated extension points.

### Code signing (optional, configurable)
`PluginsOptions.RequireSignedAssemblies` (default off) enables
`PluginAssemblyValidator`, a managed PE certificate-table parse + `SignedCms`/
`System.Security.Cryptography.Pkcs` verification against configured allowed
signer thumbprints. .NET 10 has no `X509SignatureLoader`, so the PE is read
directly.

### Frontend
`features/plugins` follows the platform conventions (ADR-006):
`PluginsService` (typed endpoints), `PluginsStore` (signalStore with per-action busy
state), and `PluginManagerComponent` (list / install via hidden file input with a
client-side 20 MB pre-check / enable / disable / uninstall with `confirm()` on the
destructive action). **Module Federation remote loading is a documented stub**: the
manifest's `remoteEntryUrl` (validated http(s)-only) is accepted and stored, but
`loadRemoteModule` wiring in the shell is deferred (see `docs/06-Not-Implemented.md`).
Plugin menu items reach the shell via `GET /api/v1/plugins/menu-items`
(permission-filtered server-side), which keeps the plugin UI surface optional.

## Alternatives Considered

### System.Runtime.Loader default context / Assembly.LoadFrom
- Pros: trivially simple.
- Cons: pins assemblies until process exit (Windows file locks), no isolation,
  contract-substitution becomes possible, no unload story at all.
- Rejected: unloading and isolation are core requirements.

### OS-level process plugins (separate plugin host process, IPC)
- Pros: strongest isolation (crash containment, CPU/memory caps).
- Cons: a second runtime, serialization at every boundary, IPluginDbContext would
  become an RPC surface — a rewrite of the whole data story; operationally a
  second service to deploy and monitor.
- Rejected for this iteration: the threat model is "untrusted but administrator-
  vetted code", not "hostile code"; ALC isolation matches that bar. Revisit if
  plugins ever come from unvetted sources.

### MEF / MEF2 composition
- Pros: built-in metadata and lazy import/export.
- Cons: no unload (MEF catalogs pin), composition model fights the restricted-
  scope requirement, and a second DI system inside a platform that standardized
  on one.
- Rejected: duplicate DI semantics.

## Consequences

- Uninstall genuinely frees disk and ALC memory; tombstone rows keep the audit trail.
- A malicious manifest cannot traverse the filesystem (zip-slip vectors covered),
  detonate a zip bomb (100 MB decompression cap), or point the UI at a
  `javascript:` remote entry (scheme allow-list).
- Plugins needing host services must ask for a future, explicit extension-point
  interface — there is no general-purpose host container access. This is deliberate.
- `IPlugin.ConfigureApp` never firing means pipeline-affecting plugins are not yet
  possible; documented as a deferred decision in `docs/06-Not-Implemented.md`.
- Collectible ALCs require the plugin (and everything it references that is not
  in the default context) to be unloadable — plugin authors must not cache host
  delegates beyond their own ALC. This constraint ships in the plugin author docs.
- The 20 MB package / 200 entry / 100 MB uncompressed caps are configurable via
  `PluginsOptions` if legitimate plugins outgrow them.
