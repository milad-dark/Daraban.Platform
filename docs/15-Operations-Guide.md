# Operations Guide — Daraban Platform (Task 8.5)

Day-two operations: every knob, what healthy looks like, what to do when it
isn't, and which screws to turn for performance. Deployment *procedure* lives
in `09-Deployment-Guide.md`; backup/restore in `10-Backup-Restore-Runbook.md`;
this guide is the reference those runbooks point at. Depth on performance
tuning lives in `07-Performance.md` — this file summarizes the knobs and
points there.

---

## 1. Environment variables reference (exhaustive)

Sources, in precedence order (later wins): `appsettings.json` →
`appsettings.{Environment}.json` → environment variables →
`DARABAN_`-prefixed variables (hosts add that prefix explicitly in
`Program.cs`). In compose, `ConnectionStrings__X` double-underscore form maps
to `ConnectionStrings:X`. Keys marked **(secret)** must never be committed —
see `10-Backup-Restore-Runbook.md` §1 for where each one lives.

### 1.1 Infrastructure (compose-provided)

| Key | Used by | Notes |
|---|---|---|
| `ConnectionStrings__Postgres` | Every `Add<Module>` (all 14 DbContexts), health check, plugin manager | `Host=postgres;Port=5432;…`. Missing/empty → that health check is skipped, not failed |
| `ConnectionStrings__Redis` | `AddDarabanDistributedCache` | Set → Redis-backed `IDistributedCache`; unset → in-process memory (single replica only) |
| `RabbitMq__Host/Port/Username/Password` | Messaging setup + RabbitMQ health check | Absent host → RabbitMQ check skipped |
| `ASPNETCORE_ENVIRONMENT` | Hosts | Must be `Production` on a server (Development enables Swagger + ephemeral JWT key) |
| `DOTNET_ENVIRONMENT` | Workers | Same rule as above |
| `DARABAN_TAG`, `DOCKERHUB_USERNAME` | CD/compose image resolution | CD rewrites `DARABAN_TAG` in server `.env` per deploy |

### 1.2 Auth & security

| Key | Used by | Notes |
|---|---|---|
| `Jwt__SigningKeyPemPath` **(secret)** | `JwtSigningKeyProvider` (both hosts share the file) | Missing outside Development = startup crash by design |
| `Jwt__Issuer` / `Jwt__Audience` | Token issuance + validation | Must match between the two hosts and the SPA's expectations |
| `Jwt__AccessTokenExpirationMinutes` (15) / `Jwt__RefreshTokenExpirationDays` (7) | `AuthService`, refresh rotation | Short access + rotating refresh; reuse revokes the family |
| `Encryption__CredentialKey` **(secret)** | SNMP credential AES-256-GCM | Base64, exactly 32 bytes; missing = startup crash |
| `Cors__AllowedOrigins` | Host.Api `Frontend` policy | Exact origins only; AgentApi uses a separate permissive policy (machines, not browsers) |

### 1.3 Performance & behavior caches

| Key | Used by | Notes |
|---|---|---|
| `PermissionCache__Ttl` / `__Jitter` | RBAC resolution | Jitter prevents thundering-herd expiry; revocations invalidate explicitly |
| `Settings__FreshnessWindow` | System settings reads | Stale-while-revalidate; Redis outage degrades to DB reads |
| `Dashboard__CacheDuration` (+ `Dashboard:*` widget options) | Per-user widget payloads | In-memory, host-wide `SizeLimit = 2048` |
| `KbCache__TtlSeconds` (60) | KB list/search results | Generation-bumped on every write; `GetById` never cached (view counter) |
| `ReportStore__RootPath` | Report file store | `/var/daraban/reports` in compose (bind-mounted volume) |
| `Plugins__RootDirectory/__MaxPackageSizeBytes/__MaxPackageEntries/__MaxUncompressedBytes/__RequireSignedAssemblies/__RequiredSignerCertificatePath` | Plugin upload + validation | Zip-slip/zip-bomb guards; size caps reject oversized packages |
| `Audit__ExcludedEntityTypes` | Audit-log interceptor | Entity types to skip recording |

### 1.4 Discovery worker tuning

`Discovery__MaxConcurrentPings` (50), `__MaxConcurrentPortScans` (100),
`__PingTimeoutMs` (2000), `__PortScanTimeoutMs` (1000), `__DeviceDelayMs`
(100). Lower these on fragile/remote networks before widening scan ranges —
a scan that floods is worse than a slow one.

### 1.5 Production overlay only (Task 8.4)

`DOMAIN`, `EMAIL`, `NGINX_HTTP_PORT` (80), `NGINX_HTTPS_PORT` (443),
`GRAFANA_ADMIN_USER`/`GRAFANA_ADMIN_PASSWORD` (first-run only),
`POSTGRES_USER/PASSWORD/DB` (reused by postgres-exporter DSN —
URL-encode `@/:?#` in the password), `BACKUP_*` (see `10-…` §3).
Full definitions in `.env.example`.

---

## 2. Health checks — what healthy looks like

Two endpoints, two different questions (both anonymous, both mapped on both
hosts; Uptime Kuma polls them from §5 of the runbook):

| Endpoint | Question it answers | Checks run | On failure |
|---|---|---|---|
| `/health/live` | "Is the process up at all?" | None — always 200 if Kestrel answers | Restart the container |
| `/health/ready` | "Can it serve traffic?" | Postgres always; Redis/RabbitMQ only where each host uses them; structured JSON per check with durations | Take out of rotation, do NOT restart (a down database isn't fixed by restarting the API) |

Interpretation rules:

- `live` 200 + `ready` 503 with postgres Unhealthy → database problem; check
  `docker compose logs postgres`, disk space (`DarabanDiskSpaceLow` may
  already be firing), then `pg_isready` from inside the container.
- `ready` 503 with redis Unhealthy → the app keeps serving (caches degrade
  to direct reads by design); fix Redis, no rollback needed.
- `ready` 503 with rabbitmq Unhealthy → event-driven paths stall (inventory
  ingestion, commands, notifications); check the broker before the app.
- Uptime Kuma watches `https://$DOMAIN/health/ready` for the keyword
  `Healthy` — that exercises the *entire edge* (DNS → nginx → TLS → host →
  dependencies), which per-endpoint container healthchecks cannot see.

---

## 3. Troubleshooting (beyond docs/09 §7)

`docs/09` §7 covers CI/CD and first-boot failures (missing secrets, JWT key,
ephemeral dev key, health-gate rollback). This section covers steady-state
failures:

| Symptom | Likely cause | Fix |
|---|---|---|
| Login 404s in UI but API directo works | Edge stripping `/api/` (pre-8.4 `nginx.conf` behavior) | Deploy current `docker/nginx.conf` / prod conf; verify `curl` preserves full path |
| 401 on every call right after deploy | `token_version` mismatch (user rows reseeded) or clock skew >30s | Check NTP on host; users re-login |
| Users locked out in waves | Brute force or a stuck client retrying; lockout is 5 fails / 15 min | Identify source IP in Serilog logs; edge rate limit already caps at 10/min |
| Scans never start / queue grows | Discovery worker down or `discovery-scan` limiter saturated | `docker compose ps`; Prometheus `DarabanQueueBackup`; worker logs |
| `/metrics` returns 404 from outside | Correct — edge must not serve it (explicit block in both nginx confs) | Scrape `host-api:8080/metrics` from inside the network |
| Prometheus target Down | Exporter or service down; or series renamed after an upgrade | `docker compose ps` → `/targets` page; re-ground names per runbook §5.2 |
| Backup log shows 0 retained / script exits 1 nightly | Empty tiers on fresh installs are handled; anything else is real | Run `--dry-run`; check `BACKUP_PASSPHRASE_FILE` perms and `BACKUP_ROOT` writability |
| TLS expires despite cron | Port 80 blocked or DNS moved; `certbot renew` failing quietly (`--quiet`) | Run renew without `--quiet` once; check `openssl s_client` dates |
| Disk filling over weeks | Prometheus 30d retention + backup accumulation + Docker overlay | `DarabanDiskSpaceLow` fires at 15%; prune per runbook §5.1 |
| Plugin install fails validation | Oversized zip, path traversal, unsigned assembly with enforcement on | Check `Plugins__*` caps; inspect server logs for the validator's reason |
| Dashboard widgets stale | Per-user cache within TTL, or generation stuck | Wait out `Dashboard__CacheDuration`; check Redis health |

Log access for all of the above: `docker compose -f docker-compose.yml
-f docker-compose.prod.yml logs -f --tail=100 [service]`; Serilog emits
structured console logs with `traceId` matching the API's `ProblemDetails`
`traceId` extension — join client reports to server logs on that id.

---

## 4. Performance tuning knobs (summary — depth in `07-Performance.md`)

Turn in this order; each step is independently reversible:

1. **Cache hit rates** — Grafana has no cache panel by default; infer from
   Postgres query rate vs request rate. If permission checks dominate DB
   load, raise `PermissionCache__Ttl` (revocations stay immediate via
   explicit invalidation). If settings reads dominate, widen
   `Settings__FreshnessWindow`.
2. **Redis vs memory** — unset `ConnectionStrings__Redis` runs a single
   replica on in-process cache (fine for staging, wrong for prod). Setting it
   is the difference between shared and per-replica caches.
3. **Database** — composite indexes from Task 8.2 are configuration-only
   until the unified migrations land; apply `CREATE INDEX CONCURRENTLY`
   manually (`07` §2). Run `EXPLAIN ANALYZE` per `07` §5 before adding more.
   Sustained connection pressure → pgBouncer (`deploy/pgbouncer/`, `07` §4).
4. **Application** — response compression is already on (brotli→gzip,
   `Fastest`); asset exports stream except XLSX (ClosedXML buffers — a SAX
   writer is the deferred fix). Discovery scan concurrency via
   `Discovery__*` (§1.4). Edge rate limits (§13 of the API reference) protect
   the login and scan paths; raise them only with Prometheus evidence, never
   on complaint alone.
5. **Frontend** — lazy-loaded routes + immutable asset caching are already
   in the frontend nginx config; no operator knobs.

What NOT to tune: JWT lifetimes (security posture, not performance),
`MaxPoolSize` past 25 without pgBouncer (more connections ≠ more Postgres
throughput), and Grafana/Alertmanager additions (alerting stays in
Prometheus + Kuma per the 8.4 scope decision).
