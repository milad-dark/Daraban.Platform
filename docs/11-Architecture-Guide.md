# Architecture Guide — Daraban Platform (Task 8.5)

Companion to the Persian `01-Software-Architecture.md`, not a replacement:
01 covers viewpoints (logical, use-case, implementation, deployment, process),
security, and non-functionals in depth. This guide covers what 01 does not —
the system as it exists *in code today*, module by module, with every claim
checkable against the repository. Where the two disagree, this file wins on
facts and 01 wins on intent; known divergences are listed in §8 rather than
silently patched into either document.

Normative detail lives in `docs/decisions/ADR-001` … `ADR-007`. This guide
summarizes them; the ADRs are the record.

---

## 1. System overview

```mermaid
flowchart TB
    subgraph EDGE["Edge (nginx, :80/:443)"]
        NX["nginx.prod.conf<br/>TLS 1.2+ · rate limits · gzip"]
    end

    subgraph UI["Browser SPA (Angular 21, standalone)"]
        FE["frontend:80<br/>auth · dashboard · assets · agents<br/>discovery · tickets · kb · plugins"]
    end

    subgraph HOSTS["ASP.NET Core hosts (.NET 10)"]
        API["Host.Api :8080<br/>user JWTs · 13 module APIs<br/>AgentStatusHub · TicketHub<br/>/metrics · /health/*"]
        AAPI["Host.AgentApi :8081<br/>agent JWTs (scopes) · inventory ingest<br/>AgentControlHub · /metrics · /health/*"]
    end

    subgraph MOD["14 modules (Api → Services → Data each)"]
        direction LR
        M1["Identity<br/>Assets<br/>ServiceDesk<br/>Financial"]
        M2["Knowledge<br/>Software<br/>Discovery<br/>Inventory"]
        M3["Automation*<br/>Notifications*<br/>Reporting<br/>Dashboard"]
        M4["Settings<br/>Plugins"]
    end

    subgraph WK["6 background workers (Generic Host)"]
        direction LR
        W1["CommandDispatch<br/>Discovery<br/>InventoryProcessor"]
        W2["NotificationDispatcher<br/>RuleEvaluator<br/>Reporting"]
    end

    subgraph INF["Infrastructure"]
        PG[("PostgreSQL 16<br/>15 schemas")]
        RD[("Redis 7<br/>cache + AOF/RDB")]
        RM[("RabbitMQ 3<br/>events + mgmt/prometheus")]
    end

    subgraph OBS["Observability (prod overlay)"]
        PR["Prometheus :9090"]
        GR["Grafana :3000"]
        KU["Uptime Kuma :3001"]
    end

    UI -->|HTTPS| NX
    AG["Daraban.Agent<br/>(fleet machines)"] -->|HTTPS| NX
    NX --> FE
    NX --> API
    NX --> AAPI
    API --> MOD
    AAPI --> MOD
    MOD -->|EF Core, schema-per-module| PG
    MOD -->|IDistributedCache| RD
    MOD -->|IEventPublisher| RM
    WK -->|consume| RM
    WK -->|EF Core| PG
    API -->|SignalR| UI
    AAPI -->|SignalR| AG
    API -->|/metrics| PR
    AAPI -->|/metrics| PR
    WK -->|":9102 each"| PR
    RM -->|":15692 plugin"| PR
    PR --> GR
end
```

`*` Automation and Notifications are registered composition roots with an
empty domain: DbContext + DI extension + AssemblyMarker, no entities or
services yet. They are deployment placeholders, not features. Anything that
lists "module capabilities" must exclude them until they grow behavior.

---

## 2. Module boundaries and responsibilities

Every module is exactly three projects — `Daraban.Modules.<Name>.{Api,Services,Data}` —
plus tests. The dependency direction is one-way: `Api → Services → Data`.
`Api` never references `Data` (enforced by `LayeringTests`), `Services` never
references another module's `Services` or `Data`, and no module references a
host. Workers reference module `Services`/`Data` only.

| Module | Schema | Owns | Status |
|---|---|---|---|
| Identity | `identity.*` | Users, profiles/rights/grants, JWT + refresh rotation, lockout, agents + credentials, agent commands, entity tree, audit logs | Full |
| Assets | `assets.*` | Assets, types/models/categories, locations, manufacturers, assignments, lifecycle + history, import/export, computers | Full |
| ServiceDesk | `servicedesk.*` | Tickets + state machine, tasks, templates, validations, costs, history, audit trail on every mutation | Full |
| Financial | `financial.*` | Budgets, contracts (+types/costs), purchases + items, suppliers, infocom + depreciation engine | Full |
| Knowledge | `knowledge.*` | Categories (tree), articles (Draft/Published/Archived), audience targets, feedback, ticket-solution links, tsvector search | Full |
| Software | `software.*` | Catalog products, licenses (seat accounting), installations, compliance checks | Full |
| Discovery | `discovery.*` | Ranges, scans, discovered devices, SNMP credentials (AES-256-GCM), discovery rules, GLPI-style import rules | Full |
| Inventory | `inventory.*` | Raw agent submissions (append-only), idempotency hashes, processing status | Ingest only — extraction lives in `InventoryProcessor` |
| Reporting | `reporting.*` | Report definitions, runs, downloads (QuestPDF/ClosedXML) | Full |
| Dashboard | `dashboard.*` | Widget catalog, per-user layouts, widget data endpoints | Full |
| Settings | `settings.*` + `core.*` | DB-backed `SystemSetting` catalog, startup seeder, connection test endpoints | Full |
| Plugins | `plugins.*` + `core.*` | Plugin registry, zip install/enable/disable, isolated `AssemblyLoadContext` loading, per-plugin schemas | Full |
| Automation | `automation.*` | (empty — composition root only) | Skeleton |
| Notifications | `notifications.*` | (empty — composition root only) | Skeleton |

Cross-cutting tables live in `core.*` (settings catalog, plugin registry).
Each module owns one `DbContext` and its own migrations + migrations-history
table inside its schema — there is no shared god-context (ADR-001).

---

## 3. Inter-module communication patterns

Four mechanisms, each with exactly one job. Using any of them for another's
job is a layering violation, not a shortcut.

| Mechanism | Used for | Example | Never for |
|---|---|---|---|
| Direct service call **within** a module | Business logic | `TicketService` → `ITicketRepository` | Calling another module's service |
| Domain events via `IEventPublisher` → RabbitMQ | Cross-module facts | `TicketCreatedEvent`, `AssetLifecycleChangedEvent`, `RawInventoryReceivedEvent`, `KbArticlePublishedEvent`, `ReportRequestedEvent` | Request/response, or anything needing an answer |
| REST between edge and hosts | All external I/O | Browser → Host.Api; agent → Host.AgentApi | Host-to-host calls (none exist) |
| SignalR hubs | Server push only | `AgentStatusHub` + `TicketHub` (Host.Api → browser); `AgentControlHub` (AgentApi → agent) | RPC-style calls; state lives in Postgres |

Event contracts live in `Daraban.Platform.Contracts` (`Agents`, `Assets`,
`Inventory`, `Knowledge`, `Reporting`, `ServiceDesk` namespaces) and are the
*only* cross-module references the architecture tests permit. A full catalog
with payload shapes:

| Event | Publisher | Meaning |
|---|---|---|
| `AgentRegisteredEvent` / `AgentDeactivatedEvent` / `AgentCredentialRevokedEvent` | Identity | Agent fleet membership changes |
| `AgentCommandPublishedEvent` | Identity | A command entered the queue (worker dispatches it) |
| `AgentCommandCompletedEvent` / `AgentCommandTimedOutEvent` | worker | Terminal command outcomes |
| `AssetCreatedEvent` / `AssetUpdatedEvent` / `AssetLifecycleChangedEvent` | Assets | Asset facts other modules react to |
| `RawInventoryReceivedEvent` | Inventory | A raw submission is stored and ready for extraction |
| `TicketCreatedEvent` / `TicketRaisedEvent` | ServiceDesk | New work entered the queue |
| `KbArticlePublishedEvent` / `KbArticleUnpublishedEvent` / `KbArticleLinkedToTicketEvent` | Knowledge | KB visibility changes; ServiceDesk stamps resolutions from the link event |
| `ReportRequestedEvent` | Reporting | Manual report generation (consumed by the Reporting worker) |

---

## 4. Hosts, workers, and shared libraries

**Host.Api (:8080)** — the user-facing host. Registers all 14 modules, serves
13 module APIs (Inventory has no browser-facing controllers; ingestion belongs
to AgentApi), both browser hubs, JWT-with-`token_version` validation, the
`auth` + `discovery-scan` rate limiters, and `/metrics`.

**Host.AgentApi (:8081)** — the machine-facing host. Registers Identity +
Inventory only, serves agent auth (`client_credentials`, no refresh tokens),
inventory ingestion, command ack/result, agent management (human admins with
user JWTs), and `AgentControlHub`. Scope-based authorization
(`agent:scope:*`), not RBAC permissions.

**Workers** (all `IHostedService` consumers, all expose `:9102/metrics`):

| Worker | Consumes | Does |
|---|---|---|
| CommandDispatch | command queue | Pushes via SignalR, enforces per-type timeouts, retries |
| Discovery | schedules | ICMP/ARP/SNMP scans per range |
| InventoryProcessor | `RawInventoryReceivedEvent` | Extracts structured devices from raw submissions |
| NotificationDispatcher | notification events | Fan-out delivery |
| RuleEvaluator | domain events | Business-rule evaluation |
| Reporting | `ReportRequestedEvent` | Generates files into the shared report store |

**Shared libraries** (`src/Shared/*`): `Common` (`Result<T>`, `Error`,
`BaseEntity`/`TenantScopedEntity`, `PagedList`, `Guard`), `Abstractions`
(`ICurrentUser`, `IEventPublisher`, `JwtOptions`, …), `Contracts` (event
catalog above), `Hosting` (Serilog, ProblemDetails, health checks,
distributed-cache selection, response compression, auth policy plumbing,
Prometheus exposition — one owner per policy per ADR-002), `Messaging`
(pure `RabbitMQ.Client` publisher/consumer infrastructure, no MassTransit).

---

## 5. Key architectural decisions and rationale

| Decision | Choice | Why (one clause each) |
|---|---|---|
| Overall shape | Modular monolith, classic 3-tier per module | Independent evolution without service-mesh ops cost (ADR-001) |
| Forbidden patterns | No DDD tactical patterns, no CQRS, no MediatR | Direct calls are traceable; ceremony without scale is cost |
| ORM / DB | EF Core 10 + Npgsql, PostgreSQL 16, schema-per-module | One engine, hard tenant-adjacent boundaries, per-module migrations |
| Primary keys | UUIDv7 everywhere | Time-ordered, index-friendly; v4 randomness fragments clustered PKs |
| Cross-module traffic | Contracts-only events over RabbitMQ.Client | Explicit coupling surface; MassTransit licensing avoided |
| User auth | Hand-rolled RS256 JWT (15 min) + rotating opaque refresh tokens | Revocable sessions without a denylist; `token_version` kills tokens instantly |
| Agent auth | OAuth2 client credentials, scope-based, no refresh | Machines re-authenticate; scopes bound at issuance |
| Authorization | Dynamic `module.action` permissions, cached in Redis | Open permission set without pre-registered policies |
| Frontend state | `@ngrx/signals` Signal Store, shared error extraction | No classic NgRx boilerplate; one error policy (ADR-006) |
| Errors | `Result<T>` + single `ToProblemResult` mapper | 22 drifted copies proved the need (ADR-003) |
| Plugins | Isolated `AssemblyLoadContext`, per-plugin schemas | Unloadable, least-privilege third-party code (ADR-007) |
| Full-text search | PostgreSQL `tsvector` generated column + GIN | No Elasticsearch dependency for KB search |
| Secrets | SNMP creds AES-256-GCM; JWT key from PEM file; backup passphrase from root-only file | Each secret's blast radius is one subsystem |
| Versions | Central `Directory.Packages.props`; .NET 10; Angular 21 | One place to bump, reproducible restores |

ADRs 001–007 in `docs/decisions/` are normative where this table summarizes.

---

## 6. Frontend architecture (map, not manual)

Angular 21, standalone components, lazy-loaded feature routes behind
`authGuard`/`guestGuard`; one Signal Store per feature (`auth`, `assets`,
`agents`, `discovery`, …); JWT in memory, refresh via HttpOnly cookie;
interceptor attaches Bearer + auto-refreshes on 401; SignalR clients for the
two browser hubs. Feature→API route map is pinned in `12-API-Reference.md`
(the `/api/v1/auth` → `/api/v1/identity/auth` contract break of Task 2.5 is
fixed and must not regress).

## 7. Deployment view (map, not manual)

Single-host Docker Compose is the supported topology: nginx (`:80/:443`)
→ hosts/frontend; Postgres/Redis/RabbitMQ internal; `docker-compose.prod.yml`
adds TLS, the monitoring stack, and hardened persistence. Procedures live in
`09-Deployment-Guide.md`; day-two operations in `10-Backup-Restore-Runbook.md`
and `15-Operations-Guide.md`.

## 8. Known documentation drift (intentionally not edited)

- `01-Software-Architecture.md` §3/§6 say five workers (six exist), show
  `KbArticle.IsPublished: bool` (now a status enum), `Budget.FiscalYear`
  (now Start/EndDate), `SoftwareLicense.TotalSeats/UsedSeats` (now
  Quantity/UsedQuantity), and §6-1 documents the old stripping `/agent/`
  routing replaced in Task 8.4. 01 is Persian; correcting it is the owner's
  call, not this task's.
- `06-Not-Implemented.md` §5 (pending unified migrations) is still accurate
  and is referenced — not contradicted — by `13-Database-Guide.md`.
