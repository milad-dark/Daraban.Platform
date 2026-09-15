# Performance (Task 8.2) — Caching, Database, Application

This document records what Task 8.2 implemented in code and what is delivered as
an operational procedure + config artifacts (per the agreed scope: pgBouncer and
EXPLAIN ANALYZE are *documented procedures*, not runtime code).

## 1. Caching

| Concern | Implementation | Configuration key |
|---|---|---|
| Distributed cache (permissions, system settings) | `AddDarabanDistributedCache` in `Daraban.Platform.Hosting`: uses **Redis** when `ConnectionStrings:Redis` is set, falls back to in-memory otherwise. Replaces the old scattered `AddDistributedMemoryCache()`. | `ConnectionStrings:Redis` |
| Permission resolver | `PermissionResolver` caches the resolved permission set per user+role fingerprint. TTL with ±jitter avoids a thundering herd on expiry; `UserService` calls `InvalidateUserAsync` on role/permission changes so revocations are immediate. | `PermissionCache:Ttl`, `PermissionCache:Jitter` |
| System settings | `SystemSettingCache`: reads served from cache within a freshness window; Redis adoption is fingerprint-guarded; a Redis outage degrades to fresh DB reads instead of failing requests. Write paths invalidate. | `Settings:FreshnessWindow` |
| Dashboard widgets | `DashboardService` caches each widget payload per user in `IMemoryCache` (bounded host-wide by `SizeLimit = 2_048`, each entry `Size = 1`). | `Dashboard:CacheDuration` |
| Knowledge-base read model | `KbArticleService` caches paged/search results in `IMemoryCache`, keyed per entity and per cache *generation*. Any create/update/status/delete/feedback bumps the generation so stale entries are unreachable until the TTL reclaims them. `GetByIdAsync` is deliberately NOT cached (view-count side effect). KB endpoints remain fully authenticated — no anonymous HTTP output-cache surface. | `KbCache:TtlSeconds` (default 60) |

## 2. Database

- **AsNoTracking**: all read-only repository queries use `.AsNoTracking()`
  (audited across every module's repository layer).
- **Composite indexes** (EF configuration in `TicketConfiguration`,
  `AssetConfiguration`; column order mirrors the hot list-query predicates —
  tenant column first, then equality filters, then sort):
  - `tickets (entity_id, status)`, `tickets (entity_id, created_at)`,
    `tickets (assigned_user_id, status)`
  - `assets` equivalents for the asset list filters.
  - ⚠ These are **configuration-only**: no migration was generated for them
    (agreed scope). They take effect when the unified migration set is created
    (see `docs/06-Not-Implemented.md` §5) or can be applied manually:
    `CREATE INDEX CONCURRENTLY ix_tickets_entity_status ON tickets (entity_id, status);` etc.
  - KB full-text **GIN** indexes already exist (see `KbArticleConfiguration`) —
    no further FTS work was needed.
- **Connection pooling**: Npgsql's built-in pool stays on (default
  `MaxPoolSize=25`). In production, put **pgBouncer** in front of Postgres —
  see §4.

## 3. Application

- **Response compression**: `AddDarabanResponseCompression` / `UseResponseCompression`
  wired in both `Host.Api` and `Host.AgentApi` before the auth stages.
  Brotli preferred, gzip fallback, enabled over HTTPS, `Fastest` compression
  level; includes `text/csv` and the XLSX MIME type.
- **Streaming exports** (replaces the old "buffer the whole table into a
  MemoryStream" approach):
  - `IAssetRepository.StreamAllAsync` returns `IAsyncEnumerable<Asset>`
    (EF `ToAsyncEnumerable` → Npgsql server-side cursor).
  - `IAssetExportService.WriteCsvAsync` writes rows into the response body as
    they arrive — peak memory is one row regardless of export size.
  - `AssetsExportController` routes every non-xlsx format through the streaming
    path. **XLSX remains buffered** because ClosedXML must build the whole
    workbook in memory before saving; a SAX-style writer would be needed to
    stream it (deferred).
- **Public output caching**: not applicable — the KB (the only candidate
  content) is tenant-scoped and behind `[RequirePermission]`; anonymous caching
  was rejected for data-leakage risk (Q2 decision).

## 4. pgBouncer deployment procedure (config artifact: `deploy/pgbouncer/`)

1. Start pgBouncer with the mounted `pgbouncer.ini` + `userlist.txt`
   (generate `userlist.txt` from `pg_shadow` — never commit it):
   ```yaml
   pgbouncer:
     image: edoburu/pgbouncer:latest
     volumes:
       - ./deploy/pgbouncer/pgbouncer.ini:/etc/pgbouncer/pgbouncer.ini:ro
       - ./secrets/pgbouncer-userlist.txt:/etc/pgbouncer/userlist.txt:ro
     ports: ["6432:6432"]
     networks: [daraban-net]
   ```
2. Point app connection strings at the pooler
   (`Host=pgbouncer;Port=6432;…`). **Migrations and DDL must connect directly
   to Postgres (port 5432)** — transaction pooling breaks prepared statements
   and session-scoped DDL.
3. Size `default_pool_size` + superuser reserve < `max_connections` on the
   Postgres server.
4. Verify routing:
   ```sql
   -- through the pooler
   SHOW transaction_isolation;  -- and: SELECT count(*) FROM pg_stat_activity;
   ```
   `SHOW POOLS;` / `SHOW STATS;` via the pgbouncer admin db.

## 5. EXPLAIN ANALYZE runbook (slow-query triage)

```sql
-- 0. enable per-session instrumentation sampling (optional):
SET track_activities = on; SET log_min_duration_statement = 500;  -- server-side: catches slow queries in logs

-- 1. find the slow statement's plan:
EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT)
SELECT ...;  -- paste the suspect query, e.g. the ticket queue:
-- SELECT * FROM tickets WHERE entity_id = $1 AND status = $2 ORDER BY created_at DESC LIMIT 20 OFFSET 0;

-- 2. what to look for:
--    • Seq Scan on a large table that should hit an index
--    • "Rows Removed by Filter" >> "Rows Returned"  → wrong/missing composite index
--    • Sort node with huge memory/disk usage        → index order not matching ORDER BY
--    • Nested Loop over a large outer              → stale statistics: run ANALYZE tickets;

-- 3. fix → index (do it CONCURRENTLY in prod):
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_tickets_entity_status
  ON tickets (entity_id, status, created_at DESC);

-- 4. re-run step 1; record before/after times in the ticket/PR.
```

Repeat `ANALYZE` after bulk data loads; `pg_stat_statements` (extension) is the
preferred way to *find* the top-N queries before profiling them.
