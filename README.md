# Daraban Platform

Daraban (دارابان) is a web-based platform for IT service management (ITSM) and IT asset
management (ITAM): service desk ticketing, asset lifecycle management, network discovery,
agent-based inventory, finance, knowledge base, software/license management, automation,
notifications, and reporting — built as a modular monolith on ASP.NET Core (.NET 10) with an
Angular frontend.

## Quick Start

```bash
# 1. Infrastructure — Postgres (5432), Redis (6379), RabbitMQ (5672, UI 15672) on localhost
docker compose up -d postgres redis rabbitmq

# 2. Bootstrap the database — creates the database itself, all 15 module schemas,
#    and every module's tables (idempotent; safe to re-run on an existing DB)
dotnet run --project tools/Daraban.Tools.DbBootstrap -- \
  "Host=localhost;Port=5432;Database=daraban;Username=daraban;Password=change_me_in_production"

# 3. Verify the port bindings are published (expect 0.0.0.0:5432->5432, 6379, 5672)
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

# 4. Backend (hosts both API hosts; modules are wired in via Program.cs)
dotnet run --project src/Host/Daraban.Host.Api          # main API  -> http://localhost:8080
dotnet run --project src/Host/Daraban.Host.AgentApi     # agent API -> http://localhost:8081

# 5. Frontend (dev server proxies /api to the backend)
cd frontend && npm install && npm start                 # -> http://localhost:4200
```

Production deployment is a single `docker compose up` (see `docker-compose.yml`; copy
`.env.example` to `.env` first and change all default secrets).

## Commands

| Command | Description |
|---|---|
| `dotnet build Daraban.Platform.sln` | Build all backend projects |
| `dotnet test Daraban.Platform.sln` | Run all backend tests (xUnit) |
| `cd frontend && npm start` | Start the Angular dev server |
| `scripts/dev-api start` / `stop` / `status` / `logs` | Run the Host API in the background on fixed port `http://localhost:8080` (`--no-launch-settings`), orphan-proof start/stop |
| `cd frontend && npm run build` | Production frontend build |
| `cd frontend && npx ng test` | Frontend tests (Karma/Jasmine) |
| `docker compose up` | Full production stack (Nginx → APIs → workers → Postgres/Redis/RabbitMQ) |
| `dotnet run --project tools/Daraban.Tools.DbBootstrap -- "<connection string>"` | Create database + all module schemas/tables on a fresh PostgreSQL instance |

### Troubleshooting

- **`The container name ... is already in use`** — a container from an older compose run
  blocks recreation. Remove it (data volumes are preserved) and start again:
  `docker rm -f daraban-postgres daraban-redis daraban-rabbitmq` then repeat Quick Start step 1.
- **Container restarts in a loop / port never binds** — check `docker logs daraban-postgres`.
  Do not swap `postgres:17` for `latest`: PG 18+ images changed the data-directory layout and
  refuse to start with the `pgdata` volume mounted at `/var/lib/postgresql/data`.
- Ports are overridable via `.env`: `POSTGRES_PORT`, `REDIS_PORT`, `RABBITMQ_PORT`,
  `RABBITMQ_MGMT_PORT`.
- **`Failed to bind ... address already in use`** when starting the API — an orphaned
  previous instance holds the port. Diagnose with `netstat -ano | findstr :<port>` and
  `taskkill /F /PID <pid>`, or just use the helper: `scripts/dev-api start` reports the
  blocking process and `scripts/dev-api stop` frees the port (it kills only processes
  verified to be `Daraban.Host.Api.exe`).

## Architecture

- **Modular monolith**: eleven domain modules under `src/Modules/*`, each with strict
  `Api → Services → Data` layering; modules never reference each other, cross-module flows
  use RabbitMQ domain events (`src/Shared/Daraban.Platform.Contracts`). See
  [ADR-001](docs/decisions/ADR-001-Modular-monolith-with-strict-module-boundaries.md).
- **Hosts**: `src/Host/*` are thin composition roots (module wiring + auth policy + CORS +
  SignalR endpoints). **Workers** (`src/Workers/*`) consume RabbitMQ and never reference
  host projects. See [ADR-004](docs/decisions/ADR-004-Workers-never-reference-hosts.md)
  and [ADR-005](docs/decisions/ADR-005-Module-composition-roots-own-all-registrations.md).
- **Single-owner policy**: JWT validation, ProblemDetails, Serilog, health checks, and
  Error→HTTP mapping each have exactly one implementation, shared by all hosts/workers.
  See [ADR-002](docs/decisions/ADR-002-Shared-infrastructure-policy-has-a-single-owner.md)
  and [ADR-003](docs/decisions/ADR-003-Domain-errors-map-to-ProblemDetails-in-one-place.md).
- **Frontend**: Angular 21 + Material, feature-owned NgRx signal stores, shared error
  handling. See [ADR-006](docs/decisions/ADR-006-Frontend-feature-stores-with-shared-error-handling.md).
- Working structure map, ownership table, and known inconsistencies:
  [ARCHITECTURE.md](ARCHITECTURE.md).
- Formal (Persian) certification documentation — architecture, requirements, test guide,
  user guide, declaration: [`docs/`](docs/README.md).

## Contributing

- One domain per module; add services to the module's `Add*Module` extension, never to a
  host's `Program.cs`.
- Controllers return `result.Error!.ToProblemResult(HttpContext)` — no private error
  helpers.
- Record significant decisions as ADRs in `docs/decisions/` (sequential `ADR-NNN`).
- Frontend: one store per feature; methods that call other store methods go in a later
  `withMethods` block; import `extractError` from `core/utils/error.util.ts`.