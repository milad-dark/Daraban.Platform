# ADR-003: Domain errors map to HTTP ProblemDetails in one place

## Status
Accepted

## Date
2026-09-07

## Context
Module services return `Result<T>` (a domain result type, not exceptions) as their contract.
Every controller therefore had to translate `Error` into an HTTP response. The translation —
a `Error.Type` → status code switch plus a `ProblemDetails` construction — had been
copy-pasted as a private `ProblemFrom(Error)` helper into 22 controllers (~130 call sites),
and the copies had drifted: the canonical `ErrorProblemDetailsExtensions.ToProblemResult`
added a `type` URI and `traceId`, the private copies silently dropped both.

The failure mode is the same as ADR-002: N copies of a policy → drift. It had already
happened.

## Decision
`Daraban.Platform.Hosting.ErrorProblemDetailsExtensions.ToProblemResult(Error, HttpContext)`
is the **only** path from a domain `Error` to an HTTP response:

- Controllers call `result.Error!.ToProblemResult(HttpContext)` directly; no private
  helpers.
- The status mapping is centralized: `NotFound → 404`, `Conflict → 409`,
  `Forbidden → 403`, `BusinessRule → 422`, `Validation/other → 400`.
- Response metadata (`traceId`, `instance`) is attached by the shared
  `AddDarabanProblemDetails` customization (ADR-002), so every response — mapped errors
  and unhandled exceptions alike — carries the same correlation fields.

## Alternatives Considered

### Keep per-controller `ProblemFrom` helpers
- Pros: Zero churn at the time.
- Cons: 22 copies already drifted once; every new controller would add a 23rd.
- Rejected.

### Throw exceptions from services and let a global exception handler map them
- Pros: Removes the `if (!result.IsSuccess) return ...` boilerplate.
- Cons: `Result<T>` is the module contract (ADR-001); services deliberately distinguish
  expected business failures from bugs. Converting to exceptions would blur that line and
  require handler-side type inspection to recover the status mapping.
- Rejected: the Result contract is the better boundary.

### A shared base controller exposing `ProblemFrom`
- Pros: Keeps the call-site name.
- Cons: Still an indirection layer; base classes accumulate unrelated helpers; the
  canonical extension is already the natural call.
- Rejected: delete the indirection rather than re-house it.

## Consequences
- All API error responses are uniform RFC 7807 documents: `type`, `title`, `status`,
  `errorCode`, `traceId`, `instance`.
- One behavior change from the previous state: responses now include `type` and `traceId`
  where the drifted helpers omitted them — a deliberate alignment, not a regression.
- New controllers follow a single, greppable pattern
  (`return result.Error!.ToProblemResult(HttpContext);`).