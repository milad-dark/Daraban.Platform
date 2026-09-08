# Daraban Platform — Architecture Notes

Working structure for the Daraban platform, recorded so later passes build with it rather
than against it. Keep this short; it is a map, not a design document.

Rationale and alternatives for the decisions below live in the Architecture Decision
Records at `docs/decisions/` (ADR-001..006); read them before re-deciding anything here.

## Boundaries and dependency direction

```
frontend (Angular)  →  Hosts (Program.cs composition roots)  →  Modules  →  Shared
                                        │                        │
                      Workers (background consumers) ────────────┘
```

- **Modules** (`src/Modules/*`): each owns one domain and exposes `Add*Module(IConfiguration)`
  as its only composition entry point. Three layers, strictly one-way:
  `*.Api (controllers) → *.Services (business logic) → *.Data (EF Core entities/repos)`.
  Modules never reference each other; cross-module communication goes through
  `Daraban.Platform.Contracts` events on RabbitMQ.
- **Hosts** (`src/Host/*`): composition roots only. They wire modules, shared infrastructure,
  auth/authorization policy providers, CORS/rate limiting/SignalR endpoints. No business logic.
- **Workers** (`src/Workers/*`): reference module Services and Shared — **never a Host project**
  (CommandDispatch used to reference Daraban.Host.AgentApi to reach the agent hub; the hub was
  moved into the Identity module to remove that inversion).
- **Shared** (`src/Shared/*`): platform-wide plumbing. Never references modules.
- **Frontend**: each feature owns its state in a feature store (`features/*/*.store.ts`),
  calls its own service, renders its own components. Auth state lives in `core/auth`.

## Who owns what (single owners)

| Policy | Owner | Consumed by |
|---|---|---|
| Domain `Error` → HTTP ProblemDetails mapping | `Daraban.Platform.Hosting.ErrorProblemDetailsExtensions.ToProblemResult` | all module controllers |
| ProblemDetails response customization + exception handler | `Daraban.Platform.Hosting.ProblemDetailsServiceCollectionExtensions.AddDarabanProblemDetails` | both hosts |
| JWT bearer validation parameters (issuer/audience/key/clock-skew) | `Daraban.Modules.Identity.Services.Auth.JwtBearerServiceCollectionExtensions.AddDarabanJwtBearer` | both hosts (Host.Api layers `token_version` revocation on top via a second options Configure) |
| Serilog setup (console + rolling file) | `Daraban.Platform.Hosting.SerilogConfigurationExtensions.UseDarabanSerilog` | both hosts, all 5 workers |
| Health checks (`/health/live`, `/health/ready`) | `Daraban.Platform.Hosting.HealthCheckExtensions` | both hosts |
| Identity module service registration (users, auth, **agent services**, commands) | `AddIdentityModule` — all Identity services register here, never in a host's Program.cs | both hosts, workers |
| Agent push channel (SignalR hub) | `Daraban.Modules.Identity.Services.Hubs.AgentControlHub` (module owns agent commands; host maps the endpoint) | AgentApi maps it; CommandDispatch pushes via `IHubContext<AgentControlHub>` |
| HTTP error → user-facing message (frontend) | `frontend/src/app/core/utils/error.util.ts` `extractError` | feature stores |
| Feature state (frontend) | per-feature `signalStore`; methods that call other store methods live in a **later** `withMethods` block (same-feature methods aren't visible on the store type) | feature components |

## Data flow (happy paths)

1. **REST**: browser → Nginx → Host.Api → module controller → service → repository → PostgreSQL.
   Auth: JWT via `AddDarabanJwtBearer`; permissions via `RequirePermission("module.action")`
   → `DynamicPermissionPolicyProvider` → `PermissionAuthorizationHandler` (Redis-cached).
2. **Agent inventory**: agent → Host.AgentApi → raw submission stored append-only
   (`RawInventorySubmission`) → `RawInventoryReceivedEvent` on RabbitMQ → InventoryProcessor
   worker → structured assets.
3. **Agent commands**: command row → `AgentCommandPublishedEvent` → CommandDispatch worker →
   `IHubContext<AgentControlHub>` push → agent reports result back over the hub or `/agent/` API.
4. **Realtime**: SignalR hubs (`AgentControlHub` agent-side, `AgentStatusHub` browser-side).

## Known inconsistencies (do not "fix" blindly)

- **Discovery controllers** (`src/Modules/Discovery/*.Api/Controllers`) use an older
  throw/try-catch error style (`BadRequest(new { error = ... })`) instead of the
  `Result` → `ToProblemResult` pattern, and `[Authorize(Policy = "admin:write")]` policies
  that no policy provider registers. Normalizing them means changing the Discovery service
  contracts — deliberately deferred.
- **Frontend does not build cleanly with `ng build`** — pre-existing (22 errors at HEAD).
  Remaining errors are in asset/agent component templates and Material imports
  (missing `CommonModule`/Material module imports, malformed template markup) plus a few
  TS issues in components; none in the stores or shared utils. Fixing is feature work.
- **Skeleton modules** (Automation, Notifications, Reporting): DbContext + DI only, no
  entities/controllers/UI. Their test projects are empty scaffolds. See
  `docs/06-Not-Implemented.md`.
- **Docker Compose** defines 3 workers while the solution has 5; only the Knowledge module
  has EF migrations. Deployment gaps tracked in `docs/06-Not-Implemented.md`.