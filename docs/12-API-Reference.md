# API Reference — Daraban Platform (Task 8.5)

The complete HTTP surface of both hosts, generated from source on 2026-09-16
(see §16 for the refresh procedure). Two hosts, two audiences:

| Host | Port (internal) | Serves | Auth model |
|---|---|---|---|
| `Host.Api` | 8080 | Browser SPA: 13 module APIs + 2 SignalR hubs | User JWT (Bearer) + `module.action` RBAC permissions |
| `Host.AgentApi` | 8081 | Fleet machines: auth, inventory ingest, commands, agent admin | Agent JWT with scopes; admin endpoints take user JWTs |

Through the edge (nginx), browser traffic arrives under `/api/...` and agent
traffic under `/api/...` too — the full controller paths below are preserved
verbatim by the proxy (see `11-Architecture-Guide.md` §8 if you remember the
old stripping behavior). Direct-to-container access uses the same paths.

Legend in the tables: **Auth** = `[Authorize]` (JWT required); a permission
name = the `[RequirePermission]` value the caller's grant must contain;
`Anonymous` = no auth — exactly six endpoints: login/register/refresh/logout,
the agent token endpoint, and the agent pre-auth `prolog` handshake (§9).

---

## 1. Host.Api — Identity & access

| Method | Path | Auth |
|---|---|---|
| POST | `/api/v1/identity/auth/register` | Anonymous |
| POST | `/api/v1/identity/auth/login` | Anonymous |
| POST | `/api/v1/identity/auth/refresh` | Anonymous (HttpOnly cookie) |
| POST | `/api/v1/identity/auth/logout` | Anonymous (clears the cookie) |
| GET | `/api/v1/identity/users?entityId&q&page&pageSize` | `identity.users.read` |
| GET | `/api/v1/identity/users/{id}` | `identity.users.read` |
| POST | `/api/v1/identity/users` | `identity.users.write` |
| PUT | `/api/v1/identity/users/{id}` | `identity.users.write` |
| POST | `/api/v1/identity/users/{id}/active` | `identity.users.write` |
| DELETE | `/api/v1/identity/users/{id}` | `identity.users.delete` |
| GET | `/api/v1/audit-logs?entity/actor/from/to&page&pageSize` | `identity.auditlogs.read` |
| GET | `/api/v1/audit-logs/{entityType}/{entityId}` | `identity.auditlogs.read` |

## 2. Host.Api — Assets

| Method | Path | Auth |
|---|---|---|
| GET | `/api/v1/assets?status&assetTypeId&locationId&search&page&pageSize` | `assets.read` |
| POST | `/api/v1/assets` | `assets.write` |
| GET | `/api/v1/assets/{id}` | `assets.read` |
| PUT | `/api/v1/assets/{id}` | `assets.write` |
| DELETE | `/api/v1/assets/{id}` | `assets.delete` |
| GET | `/api/v1/assets/export?format=csv\|xlsx&…` | `assets.read` |
| POST | `/api/v1/assets/import?dryRun` (multipart) | `assets.write` |
| GET | `/api/v1/assets/import/template` | `assets.read` |
| GET/POST | `/api/v1/asset-types`, `/api/v1/asset-types/{id}` (+PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/asset-categories`, `/api/v1/asset-categories/{id}` (+PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/locations`, `/api/v1/locations/{id}` (+PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/manufacturers`, `/api/v1/manufacturers/{id}` (+PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/assets/{assetId}/assignments` | `assets.read` / `assets.write` |
| GET/DELETE | `/api/v1/assets/{assetId}/assignments/current` | `assets.read` / `assets.write` |
| GET | `/api/v1/users/{userId}/assets`, `/api/v1/departments/{departmentId}/assets` | `assets.read` |
| POST | `/api/v1/assets/{assetId}/lifecycle/transition` | `assets.write` |
| GET | `/api/v1/assets/{assetId}/lifecycle/history` | `assets.read` |

## 3. Host.Api — ServiceDesk

| Method | Path | Auth |
|---|---|---|
| GET | `/api/v1/tickets?type&status&priority&assignedUserId&assignedGroupId&search&page&pageSize` | `servicedesk.read` |
| POST | `/api/v1/tickets` | `servicedesk.write` |
| GET | `/api/v1/tickets/{id}` | `servicedesk.read` |
| PUT | `/api/v1/tickets/{id}` | `servicedesk.write` |
| DELETE | `/api/v1/tickets/{id}` (New/Cancelled only) | `servicedesk.delete` |
| PUT | `/api/v1/tickets/{id}/status` | `servicedesk.write` |
| PUT | `/api/v1/tickets/{id}/assign` | `servicedesk.write` |
| PUT | `/api/v1/tickets/{id}/escalate` | `servicedesk.write` |
| PUT | `/api/v1/tickets/{id}/solve` (solution text required) | `servicedesk.write` |
| PUT | `/api/v1/tickets/{id}/close` (Solved only) | `servicedesk.write` |
| GET | `/api/v1/tickets/{id}/history` | `servicedesk.read` |
| GET | `/api/v1/tickets/count/open`, `/api/v1/tickets/count/overdue` | `servicedesk.read` |
| GET/POST | `/api/v1/tickets/{ticketId}/tasks` | read / write |
| DELETE | `/api/v1/tickets/{ticketId}/tasks/{id}` (own tasks only) | `servicedesk.write` |
| GET/POST | `/api/v1/ticket-templates`, `/api/v1/ticket-templates/{id}` (+PUT/DELETE) | read / write / delete |

## 4. Host.Api — Financial

Route note: these five controllers use `[Route("api/[controller]")]`, so the
paths keep the controller's PascalCase plural (`/api/Budgets`, not
`/api/budgets`). Every other module uses lowercase kebab-case — this
inconsistency is real, documented here, and a rename candidate (it would break
existing clients, so it needs a versioned migration, not a silent fix).

| Controller | Extra actions beyond CRUD | Auth |
|---|---|---|
| `/api/Budgets` | `GET /summary` (totals across all budgets) | `financial.read` / write / delete |
| `/api/Contracts` | `POST /{id}/status` (Draft→Active→Suspended/Expired/Cancelled) | `financial.read` / write / delete |
| `/api/Purchases` | `POST /{id}/status`, `POST /{id}/items`, `DELETE /{id}/items/{itemId}` | `financial.read` / write / delete |
| `/api/Suppliers` | — | `financial.read` / write / delete |
| `/api/Infocoms` | `GET /asset/{assetId}`, `GET /{id}/depreciation` | `financial.read` / write / delete |

All five support `GET` (paged + filters), `GET /{id}`, `POST`, `PUT /{id}`,
`DELETE /{id}` with the matching `financial.*` permission.

## 5. Host.Api — Knowledge

| Method | Path | Auth |
|---|---|---|
| GET | `/api/v1/kb/articles?categoryId&status&isFaq&authorUserId&title&page&pageSize` | `knowledge.read` |
| GET | `/api/v1/kb/articles/search?q&categoryId&status&page&pageSize` (ts_rank ordered) | `knowledge.read` |
| GET | `/api/v1/kb/articles/{id}?countView=` | `knowledge.read` |
| POST | `/api/v1/kb/articles` (always born Draft) | `knowledge.write` |
| PUT | `/api/v1/kb/articles/{id}` (not when Archived) | `knowledge.write` |
| POST | `/api/v1/kb/articles/{id}/status` (Draft↔Published→Archived) | `knowledge.publish` |
| DELETE | `/api/v1/kb/articles/{id}` | `knowledge.delete` |
| POST | `/api/v1/kb/articles/{id}/feedback` (any reader may rate) | `knowledge.read` |
| GET | `/api/v1/kb/articles/{id}/feedback` | `knowledge.write` |
| GET/POST | `/api/v1/kb/categories[?tree=true]`, `/api/v1/kb/categories/{id}` (+PUT/DELETE) | read / write / delete |
| GET | `/api/v1/tickets/{ticketId}/kb-links` | `servicedesk.read` |
| POST | `/api/v1/tickets/{ticketId}/solution` | `servicedesk.write` |
| DELETE | `/api/v1/tickets/{ticketId}/kb-links/{articleId}` | `servicedesk.write` |

## 6. Host.Api — Software

| Method | Path | Auth |
|---|---|---|
| GET/POST | `/api/v1/softwares`, `/api/v1/softwares/{id}` (+PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/software-licenses`, `/api/v1/software-licenses/{id}` (+PUT/DELETE) | read / write / delete |
| GET | `/api/v1/software-licenses/{id}/compliance` | `software.read` |
| GET | `/api/v1/software-licenses/software/{softwareId}` | `software.read` |
| GET/POST | `/api/v1/software-installations`, `/api/v1/software-installations/{id}` | read / write |
| POST | `/api/v1/software-installations/{id}/uninstall` | `software.write` |
| GET | `/api/v1/software-installations/asset/{assetId}` (+`/summary`) | `software.read` |
| GET | `/api/v1/software-installations/software/{softwareId}` | `software.read` |

## 7. Host.Api — Discovery

| Method | Path | Auth |
|---|---|---|
| GET/POST | `/api/v1/discovery/ranges`, `/api/v1/discovery/ranges/{id}` (+PUT/DELETE) | read / write / delete |
| POST | `/api/v1/discovery/ranges/{id}/scan` | `discovery.write` + `discovery-scan` rate policy |
| GET | `/api/v1/discovery/scans/recent`, `/scans/range/{rangeId}`, `/scans/{id}`, `/scans/{id}/devices` | `discovery.read` |
| GET | `/api/v1/discovery/devices/recent`, `/ranges/{rangeId}/devices` | `discovery.read` |
| GET/POST | `/api/v1/discovery/credentials`, `/credentials/{id}` (+PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/discovery/rules`, `/rules/{id}` (+PUT/DELETE) | read / write / delete |
| GET/POST | `/api/v1/discovery/import-rules`, `/import-rules/{id}` (+PUT/DELETE) | read / write / delete |
| GET | `/api/v1/discovery/import-rules/active` | `discovery.read` |
| POST | `/api/v1/discovery/import-rules/evaluate` | `discovery.write` |
| GET | `/api/v1/discovery/import-rules/fields`, `/operators`, `/action-types` | `discovery.read` |
| GET | `/api/v1/discovery/dashboard` | `discovery.read` |

## 8. Host.Api — Dashboard / Settings / Plugins / Reporting

| Method | Path | Auth |
|---|---|---|
| GET | `/api/v1/dashboard/widgets` | `dashboard.read` |
| GET/PUT | `/api/v1/dashboard/layout` | read / `dashboard.write` |
| GET | `/api/v1/dashboard/data/{widgetType}` | `dashboard.read` |
| GET | `/api/v1/settings` | `settings.read` |
| PUT | `/api/v1/settings/{key}` | `settings.write` |
| POST | `/api/v1/settings/test/{key}` | `settings.write` |
| GET/POST | `/api/v1/plugins`, `/api/v1/plugins/{pluginId}` (+DELETE) | read / write |
| POST | `/api/v1/plugins/{pluginId}/enable`, `.../disable` | `plugins.write` |
| GET | `/api/v1/plugins/menu-items` | `plugins.read` |
| GET | `/api/v1/reports/definitions`, `/api/v1/reports/my` | `reports.read` |
| POST | `/api/v1/reports/definitions` | `reports.manage` |
| POST | `/api/v1/reports/{id}/generate` | `reports.manage` |
| GET | `/api/v1/reports/{id}/runs`, `/api/v1/reports/{id}/download` | `reports.read` |
| GET | `/api/v1/agents`, `/api/v1/agents/{id}`, `/summary` (admin fleet view) | `Auth` (+ admin grants) |
| GET | `/api/v1/agents/{id}/inventory`, `/jobs` | `Auth` (+ admin grants) |

## 9. Host.AgentApi — machine surface

Token endpoint is intentionally anonymous (you cannot present a token to get
a token); everything else requires an agent JWT carrying the named scope.
Scope checks are `agent:scope:*` policies, not RBAC permissions — a machine
identity never resolves `module.action` grants.

| Method | Path | Auth |
|---|---|---|
| POST | `/api/v1/agents/auth/token` (client_id + client_secret → JWT) | Anonymous |
| GET | `/api/agent/prolog?deviceId` (pre-auth handshake) | Anonymous — by explicit `[AllowAnonymous]`, see below |
| POST | `/api/agent/inventory`, `/discovery`, `/netinventory`, `/esx`, `/wakeonlan` | `agent:scope:inventory:write` |
| POST | `/api/agent/deploy/result` | agent scope |
| GET | `/api/agent/deploy/jobs?deviceId` | agent scope |
| GET | `/api/agent/commands/pending` | agent scope (own commands only) |
| POST | `/api/agent/commands/{id}/acknowledge`, `/{id}/result` | agent scope (own commands only) |
| GET | `/api/agent/commands/{id}/result` | agent scope |
| *admin* | `/api/v1/agents*`, `/api/v1/agents/{id}/credentials`, `/audit`, `/commands` | User JWT + admin grants |

`prolog` is the one non-login anonymous endpoint in the system, and it is
anonymous *explicitly* (method-level `[AllowAnonymous]` overriding the
controller's scope policy), because agents call it before they hold any
token. It accepts only a device id and returns non-sensitive server
parameters — no data access, no state change. Two honesty notes, both marked
`TODO (Task 5.x)` in code: `prolog` currently always answers `known: false`
(a placeholder, not a lookup), and `deploy/jobs` always answers `[]`. If
either grows beyond that, re-examine the anonymity first.

Inventory has no module-Api controllers of its own: ingestion lives here.

## 10. SignalR hubs (server push, not RPC)

| Hub | Host / path | Direction | Purpose |
|---|---|---|---|
| `AgentStatusHub` | Api `/hubs/agent-status` | → browser | Live agent presence for the fleet dashboard |
| `TicketHub` | Api `/hubs/tickets` | → browser | Live ticket updates (see frontend `ticket-hub.service`) |
| `AgentControlHub` | AgentApi `/hubs/agent-control` | → agent | Command dispatch push to fleet machines |

Hub negotiation is JWT Bearer like any other endpoint (query-string token for
WebSocket upgrades). Timeouts at the edge are 7 days — hub connections live
for hours; the 60s default would kill them silently.

## 11. Authentication flows

**User (interactive).** Register → Login returns `{ accessToken,
accessTokenExpiresAt, user }` and sets the refresh token as an HttpOnly,
SameSite=Strict cookie scoped to `/api/v1/identity/auth`. Access tokens live
15 minutes and carry `sub`, `active_entity_id`, `name`, `email`,
`token_version`. Refresh rotates the opaque token server-side (reuse of a
rotated token revokes the whole family — theft response, not an error to
retry). Logout revokes the family and clears the cookie.

```bash
# Register + login (frontend does exactly this after the Task 8.4 URL fix)
curl -s -X POST https://$DOMAIN/api/v1/identity/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"username":"alice","email":"alice@example.com","password":"correct-horse-battery-staple-1","displayName":"Alice"}'
curl -s -c cookies.txt -X POST https://$DOMAIN/api/v1/identity/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"usernameOrEmail":"alice","password":"correct-horse-battery-staple-1"}'
# Call something real with the access token from the login response:
curl -s https://$DOMAIN/api/v1/tickets/count/open -H "Authorization: Bearer $ACCESS"
# Refresh (cookie does the work):
curl -s -b cookies.txt -c cookies.txt -X POST https://$DOMAIN/api/v1/identity/auth/refresh
```

Account safety nets, all server-side: 5 failures → 15-minute lockout (counter
resets when the window lapses); disabled/deleted accounts fail closed on both
login *and* refresh (the rotated replacement is revoked, not handed over);
bumping `TokenVersion` invalidates every outstanding access token on next
request; admin-created accounts have no password hash and cannot log in until
one is set.

**Agent (machine).** `client_credentials` with no refresh tokens — agents
re-authenticate before expiry (60s early, double-checked locking).

```bash
curl -s -X POST https://$DOMAIN/api/v1/agents/auth/token \
  -H 'Content-Type: application/json' \
  -d '{"clientId":"da_…","clientSecret":"sk_…","scope":"inventory:write"}'
# → { accessToken, tokenType: "Bearer", expiresIn, scope }
```

The issued JWT carries `sub` (agent id), `agent_type`, `scope` (intersection
of requested ∩ credential ∩ agent-allowed; partial matches are denied, not
downgraded), `is_agent=true`, `active_entity_id`. Unknown client and wrong
secret return byte-identical 403s (no enumeration oracle). Revocation =
deactivate the credential or the agent; a wildcard credential resolves
*before* intersecting, so `*` means "everything this agent may do".

## 12. Errors — one shape everywhere

Every module service returns `Result<T>`; every controller maps it through
the single shared `ErrorProblemDetailsExtensions.ToProblemResult`
(ADR-003). The wire shape is RFC 7807:

```json
{
  "title": "Ticket not found.",
  "status": 404,
  "type": "https://daraban.local/errors/notfound",
  "errorCode": "TICKET.NOT_FOUND",
  "traceId": "00-…"
}
```

| `ErrorType` | HTTP | Meaning |
|---|---|---|
| `Validation` | 400 | Malformed input (FluentValidation + service guards) |
| `NotFound` | 404 | Unknown id (never leaks *whose* id was wrong beyond that) |
| `Conflict` | 409 | Duplicate unique value (name, tag, order number, install) |
| `Forbidden` | 403 | Authenticated but not allowed (cross-tenant, locked, revoked credential) |
| `BusinessRule` | 422 | Well-formed request the state machine rejects |

`errorCode` convention: `MODULE.SCREAMING_SNAKE` (`IDENTITY.*`, `ASSETS.*`,
`TICKET.*`, `TICKET_TASK.*`, `TICKET_TEMPLATE.*`, `KNOWLEDGE.*`,
`SOFTWARE.*`, `LICENSE.*`, `INSTALLATION.*`, `BUDGET.*`, `CONTRACT.*`,
`SUPPLIER.*`, `PURCHASE.*`, `INFOCOM.*`, `AGENTS.*`, `INVENTORY.*`).
Discovery is the exception that proves the rule: its services throw
`InvalidOperationException`, which the global handler renders as 500
*without* an `errorCode` — structured errors there are a follow-up, and any
client code must treat a 500 from `/api/v1/discovery/*` as opaque.

## 13. Rate limits (two layers, same budgets)

| Layer | Policy | Budget | Applies to |
|---|---|---|---|
| ASP.NET (`Program.cs`) | `auth` | 10 req/min, reject-over-limit | `identity/auth/*` (via `[EnableRateLimiting]`) |
| ASP.NET | `discovery-scan` | 5/min + queue 2 | scan start |
| nginx (prod) | `auth` zone | 10 req/min/IP, burst 5 | `/api/v1/identity/auth/` |
| nginx (prod) | `agent_auth` zone | 60 req/min/IP, burst 10 | `/api/v1/agents/auth` |
| nginx (prod) | `api` zone | 100 req/s/IP, burst 200 | everything else |

Overflow answers **429** at both layers. The edge and the app enforce the
same login budget so neither is a single point of bypass; the integration
fixture neutralizes the in-app limiter (shared loopback IP) so parallel tests
never flake on 429s.

## 14. Pagination, filtering, and envelope shapes

List endpoints take `page` (≥1, default 1) and `pageSize` (default 20, hard
cap 200 — larger values are clamped, not rejected). An unclamped `page=0`
used to produce `Skip(-pageSize)` and a Postgres error; every service now
normalizes. Responses share one envelope:

```json
{ "items": [ … ], "totalCount": 137, "page": 2, "pageSize": 20 }
```

Filters are per-resource query params (`status`, `categoryId`, `isFaq`,
`supplierId`, … — see tables above); free-text search is substring match
except KB search, which is relevance-ranked full-text (`ts_rank`,
`websearch_to_tsquery`, GIN-indexed). Sort orders are endpoint-fixed
(newest-first for feeds, name order for catalogs, sort-order for trees).

## 15. Swagger / OpenAPI export

Both hosts serve Swagger UI in Development only. To export the contract:

```bash
# Against a local stack (dev compose is fine -- Swagger needs no auth to read):
curl -s http://localhost:8080/swagger/v1/swagger.json -o openapi.host-api.json
curl -s http://localhost:8081/swagger/v1/swagger.json -o openapi.host-agentapi.json
```

The Angular client is generated from these files — regenerate, don't hand-edit,
when the backend changes.

## 16. Refresh procedure for this document

This file is generated, not written: `docs/gen-endpoints.ps1` walks every
`*Controller.cs`, associates each action with its attribute block (`[HttpX]`,
`[RequirePermission]`, `[AllowAnonymous]`, rate-limit policy), and emits
`module | METHOD | path | auth`. After any controller change, regenerate and
diff: a new row with empty auth is a missing `[Authorize]` until proven
otherwise. AgentApi scope policies (`agent:scope:*`) and Financial's
`[controller]`-token routes need the manual annotations in §4 and §9 --
the generator reports them raw.
