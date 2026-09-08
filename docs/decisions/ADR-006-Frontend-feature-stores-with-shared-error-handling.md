# ADR-006: Frontend feature stores with shared error handling

## Status
Accepted

## Date
2026-09-07

## Context
The Angular frontend grew feature-by-feature (auth, dashboard, assets, agents, discovery),
and each feature owns its state in an NgRx `signalStore` (auth state in `core/auth`).
Two problems accumulated:

1. **Duplicated, drifted policy.** `extractError` — "turn an unknown HttpErrorResponse into
   a user-facing message" — was copied into three stores, and the auth copy drifted its
   fallback text (`'...Please try again.'` vs `'An unexpected error occurred.'`). Same
   failure mode as ADR-002/ADR-003, at the frontend scale.
2. **Methods that call other methods did not type-check.** Inside a single `withMethods`
   block, the `store` parameter's type includes only state and computed values from
   earlier features — methods defined in the *same* block are not visible on its type.
   The assets/agents stores had `setPage` calling `store.loadAssets()` and
   `dispatchCommand` calling `store.loadCommandHistory()`, so `ng build` failed with
   `TS2339: Property 'loadAssets' does not exist ...`.

## Decision
- **Shared error extraction:** `frontend/src/app/core/utils/error.util.ts` exports
  `extractError(err: unknown): string` and is the only implementation; the three stores
  import it. Backend ProblemDetails responses (`detail`/`title`) drive the message, with a
  stable fallback.
- **Store composition rule:** methods that call other store methods live in a **later**
  `withMethods` feature, where the accumulated store type includes the earlier methods.
  Within one block, methods only read state/computed and call services. Runtime behavior is
  identical — this is purely a typing/correctness fix.
- State ownership is unchanged: each feature's store owns that feature's state; services
  own HTTP; components only call store methods.

## Alternatives Considered

### One global application store
- Pros: Single place for everything.
- Cons: Features are independent and mostly read their own data; a global store couples
  them and grows without bound. The features do not share state today.
- Rejected.

### A shared "base store" factory abstracting pagination/loading/error
- Pros: Less per-store boilerplate.
- Cons: Features differ enough (list + detail + commands + inventory in agents, reference
  data in assets) that a factory would need feature flags or callbacks — ceremony that
  obscures the actual differences. The stores are each ~200 lines and readable alone
  (ADR-001's "boundaries are for clarity, not ceremony").
- Rejected: extract only what is *literally* identical (error extraction), not what is
  merely similar.

### Leave the same-block `store.method()` calls and suppress the type error
- Pros: No change.
- Cons: `ng build` was red; suppressing hides a real correctness hazard (at runtime the
  method may exist, but nothing guarantees it once the store is refactored).
- Rejected.

## Consequences
- Feature stores type-check; `ng build` errors from the stores are gone (remaining
  frontend build errors are pre-existing template/Material issues in components, tracked in
  `ARCHITECTURE.md` and `docs/06-Not-Implemented.md`).
- Error messages are consistent across auth, assets, and agents.
- The pattern for future features: one store per feature; split `withMethods` when methods
  call each other; import `extractError` rather than copying it.