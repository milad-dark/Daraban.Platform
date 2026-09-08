# ADR-002: Shared infrastructure policy has a single owner

## Status
Accepted

## Date
2026-09-07

## Context
Before this decision, the same infrastructure policy was copy-pasted across entry points and
had already drifted:

- Both hosts (`Daraban.Host.Api`, `Daraban.Host.AgentApi`) carried an identical inline
  `AddProblemDetails` customization block and an identical JWT bearer validation block
  (issuer, audience, 30s clock skew, RSA signing key).
- All five workers re-implemented their own *simpler* Serilog bootstrap
  (`new LoggerConfiguration().ReadFrom.Configuration(...)`) instead of using the shared
  `UseDarabanSerilog` extension, which its own doc comment said they were supposed to use.
  The copies silently diverged (no enrichers, no file sink, no min-level overrides).

The failure mode is drift: two copies of a policy are two places to update, and the second
update gets forgotten.

## Decision
Each infrastructure policy has exactly one owner, implemented as a registration extension:

| Policy | Owner | Consumers |
|---|---|---|
| JWT bearer validation (issuer/audience/key/clock-skew) | `Daraban.Modules.Identity.Services.Auth.JwtBearerServiceCollectionExtensions.AddDarabanJwtBearer` | both hosts |
| ProblemDetails customization + exception handler | `Daraban.Platform.Hosting.ProblemDetailsServiceCollectionExtensions.AddDarabanProblemDetails` | both hosts |
| Serilog setup (console + rolling file, enrichers) | `Daraban.Platform.Hosting.SerilogConfigurationExtensions.UseDarabanSerilog` | both hosts, all five workers |
| Health checks (`/health/live`, `/health/ready`) | `Daraban.Platform.Hosting.HealthCheckExtensions` | both hosts |

Host-specific behavior is layered **on top** of the shared policy, never forked:
`Daraban.Host.Api` adds its `token_version` revocation check as a second
`Configure<JwtBearerOptions>` action that runs after the shared one.

The JWT extension lives in the Identity module rather than Shared because it needs
`JwtSigningKeyProvider` (which Identity owns); placing it in `Daraban.Platform.Hosting`
would invert the Shared → Module dependency.

## Alternatives Considered

### Keep per-host/per-worker copies, fix them as noticed
- Pros: No new abstractions.
- Cons: This is exactly what caused the drift; "fix as noticed" is not a policy.
- Rejected: the copies had already diverged in ways that mattered (worker logs lacked the
  enrichers/file sink hosts had).

### Put `AddDarabanJwtBearer` in `Daraban.Platform.Hosting`
- Pros: One shared home for all host plumbing.
- Cons: Requires `Daraban.Platform.Hosting` → `Daraban.Modules.Identity.Services`
  reference, inverting the Shared/Module dependency direction (see ADR-001).
- Rejected: module boundaries are the stronger constraint.

### Pass the signing key provider in as a delegate parameter
- Pros: Keeps the extension in Shared without a module reference.
- Cons: An untyped `Func<IServiceProvider, ...>` seam that every caller must wire; the
  Identity module is the natural owner of token policy anyway.
- Rejected: ceremony with no clarity gain.

## Consequences
- Changing auth or logging policy means changing exactly one file; all hosts and workers get
  it on next deploy.
- Worker logs now match host logs (enrichers, rolling file, retention) — a deliberate
  behavior alignment, not a regression.
- `RequireHttpsMetadata` stays a per-host parameter derived from the environment, since it
  legitimately differs between development and production.