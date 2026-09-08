# ADR-005: Module composition roots own all service registrations

## Status
Accepted

## Date
2026-09-07

## Context
Each module exposes `Add*Module(IConfiguration)` as its only composition entry point
(ADR-001). The Identity module's extension registered users, auth, tokens, permissions, and
command services — but **not** the agent services (`IAgentService`, `IAgentAuthService`,
`IAgentRepository`). Those three were registered ad-hoc in `Daraban.Host.AgentApi`'s
`Program.cs`.

Meanwhile `Daraban.Host.Api`'s `AdminAgentController` (the agent fleet dashboard behind the
Angular UI) constructor-injects `IAgentService` and `IAgentCommandRepository` — and
`Daraban.Host.Api` never registered them. The project compiled (the types resolve via the
Identity references) but the endpoints would fail at runtime with an unregistered-service
exception on first request. The bug was invisible until the endpoint was actually hit.

Root cause: registration policy lived in the wrong place. A host is a composition root, not
a place to decide which module services exist; that decision belongs to the module.

## Decision
`Add*Module(IConfiguration)` registers **all** of that module's services — every service
interface, repository, and the DbContext. Hosts only call module extensions and wire
cross-cutting infrastructure (auth policy providers, CORS, rate limiting, SignalR endpoints,
health endpoints). Specifically, `AddIdentityModule` now registers the agent services
(`IAgentRepository`, `IAgentService`, `IAgentAuthService`, `IAgentCommandRepository`,
`IAgentCommandService`) alongside the rest, and `Daraban.Host.AgentApi`'s inline
registrations were deleted.

Rule of thumb: if a service is needed by more than one host/worker, it is registered by its
module, not by any single host.

## Alternatives Considered

### Duplicate the registrations in every host that needs them
- Pros: No change to module extensions.
- Cons: This is precisely how the bug happened — one host got them, the other didn't, and
  nothing complained until runtime. N hosts → N chances to forget.
- Rejected.

### Split agent registrations into a separate `AddIdentityAgentServices` extension called by both hosts
- Pros: Keeps the identity module extension small.
- Cons: Two entry points for one module means callers must remember both; the same
  forget-one bug reappears in a new shape.
- Rejected: one module, one registration entry point.

## Consequences
- Every host or worker that calls `AddIdentityModule` gets a complete, working Identity
  service graph. `Daraban.Host.Api`'s agent dashboard endpoints now resolve their
  dependencies — the latent runtime failure is fixed.
- Registration code is discoverable in one file per module; new services are added by
  editing the module extension, not each host's `Program.cs`.
- Host `Program.cs` files stay thin: they declare cross-cutting policy (ADR-002) and map
  endpoints, and nothing else.