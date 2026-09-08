# ADR-001: Modular monolith with strict module boundaries

## Status
Accepted

## Date
2026-09-07

## Context
Daraban is a platform covering eleven domains (identity, assets, service desk, financial,
knowledge, software, discovery, inventory, automation, notifications, reporting) that must
ship as one deployable web platform. The domains are related but evolve at different paces,
are built by different people over time, and each carries enough behavior that mixing them
in a single layer would be unmaintainable. At the same time, the whole platform is deployed
and operated as one system, so the cost of independent services is not justified.

Key requirements:
- Domains must be independently evolvable, testable, and reusable.
- Cross-domain coupling must be explicit and visible.
- Deployment remains a single platform (Docker Compose), not a service mesh.

## Decision
Build a **modular monolith**:

- Each domain is a module under `src/Modules/*` with exactly three layers, depending
  one-way: `*.Api` (controllers) → `*.Services` (business logic) → `*.Data`
  (EF Core entities/repositories/DbContext).
- Modules **never reference each other**. Cross-module communication happens only through
  `Daraban.Platform.Contracts` domain events, published to RabbitMQ and consumed by workers
  (e.g. `RawInventoryReceivedEvent` from Inventory consumed by the inventory processor).
- Each module exposes one composition entry point, `Add*Module(IConfiguration)`, which
  registers its DbContext, repositories, services, and validators (see ADR-005).
- Shared plumbing (ProblemDetails, health checks, Serilog, messaging) lives in
  `src/Shared/*` and is owned by exactly one component each (see ADR-002).

## Alternatives Considered

### Microservices
- Pros: Independent scaling and deployment per domain.
- Cons: The platform runs as one product on one network; per-service ops (deploy, monitor,
  version) would multiply without a scaling need. Cross-domain transactions become
  distributed. RabbitMQ event plumbing would be the same.
- Rejected: operational cost with no matching benefit at this scale.

### Single application with namespaces only
- Pros: No project-boundary ceremony, fastest to write.
- Cons: Nothing stops Services from reaching into another domain's repositories; the
  "layers" become advisory and drift within weeks. The layering tests in
  `tests/ArchitectureTests` could not express the rule.
- Rejected: boundaries exist for clarity; without enforced project boundaries there are no
  boundaries.

### Database-as-integration (shared schema, direct table access)
- Pros: Simple data sharing between domains.
- Cons: Creates hidden coupling at the schema level; no event trail; hard to test domains in
  isolation.
- Rejected: events make the coupling explicit and auditable.

## Consequences
- Modules can be built, tested, and reasoned about in isolation; each has its own test
  project.
- Cross-module flows are asynchronous (RabbitMQ) — eventual consistency between modules by
  design; a module can never block on another module's synchronous call.
- The composition root (host `Program.cs`) must reference every module; adding a module
  means wiring it into the hosts and Docker Compose.
- The strict dependency rules are enforced by `tests/ArchitectureTests` (wiring to
  NetArchTest is in progress — see `docs/06-Not-Implemented.md`).