# ADR-004: Workers never reference host projects

## Status
Accepted

## Date
2026-09-07

## Context
The `Daraban.Workers.CommandDispatch` worker pushes queued agent commands to connected
agents over SignalR using `IHubContext<AgentControlHub>`. `AgentControlHub` lived in
`Daraban.Host.AgentApi.Hubs` — so the worker referenced the **host project**
(`Daraban.Host.AgentApi.csproj`) to get one hub type.

This inverts the dependency direction the architecture intends (ADR-001): workers are
background consumers of module services, and hosts are composition roots. A worker that
compiles against a host carries the whole web host's transitive graph (controllers, CORS
setup, launch settings) into a process that only wants to consume RabbitMQ and push over
SignalR, and it makes the host a compile-time prerequisite of the worker.

The hub itself had no host-specific dependencies: it only used Identity services
(`IAgentCommandService`, `IAgentService`), shared contracts (`Daraban.Platform.Contracts.Agents`),
and ASP.NET SignalR types.

## Decision
Dependency direction is enforced: **workers reference module services and Shared, never a
host project.** Concretely, `AgentControlHub` moved from `Daraban.Host.AgentApi.Hubs` into
`Daraban.Modules.Identity.Services.Hubs` — the module that owns agent commands and their
DTOs. The AgentApi host maps the endpoint (`app.MapHub<AgentControlHub>("/hubs/agent-control")`)
exactly as before; CommandDispatch now resolves `IHubContext<AgentControlHub>` from the
Identity module it already referenced, and its `Daraban.Host.AgentApi` project reference was
deleted.

`AgentStatusHub` (browser-side fleet status, consumed only by the web UI) stays in
`Daraban.Host.Api` — it has no worker consumer, so no inversion exists there.

## Alternatives Considered

### Move the hub to `Daraban.Platform.Hosting` (Shared)
- Pros: A single "hubs" home for the platform.
- Cons: The hub depends on Identity module services (`IAgentCommandService`,
  `IAgentService`), so Shared would acquire a Module reference — the exact inversion the
  change is eliminating (Shared must never reference modules, ADR-001).
- Rejected.

### Keep the host reference and accept it
- Pros: No movement.
- Cons: Workers stay coupled to a web host's compile graph; the boundary stays wrong and
  every future worker tempted to push over SignalR copies the pattern.
- Rejected.

## Consequences
- The dependency graph is acyclic and direction-correct: `frontend → hosts → modules →
  shared` and `workers → modules/shared`, with no worker → host edge.
- CommandDispatch's project graph shrank (no web host transitive dependencies); it now
  carries only Identity services, Messaging, and Hosting.
- The hub's home is a module; any host (or future worker) can map/consume it by referencing
  the module. The pattern for future hubs: put them next to the domain behavior they
  expose, not in a host.