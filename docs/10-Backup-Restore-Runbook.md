# Backup & Restore Runbook — Daraban Platform on Linux (Task 8.4)

How the deployment is observed, how its data survives, and how to bring it
back. Companion to `docs/09-Deployment-Guide.md` (which gets you *deployed*);
this gets you *back* when something breaks. All paths below assume the
production checkout at `/opt/daraban` with `docker-compose.prod.yml` active.

> First-deploy checklist lives at the bottom (§8). If you are setting up a
> fresh server, read §1–§3 and §8; the rest is for incident day.

---

## 1. Architecture recap (what runs where)

```
internet ──:80/443──▶ nginx ──┬──▶ host-api:8080 ──▶ postgres / redis / rabbitmq
                               ├──▶ host-agentapi:8081 ──┤
                               └──▶ frontend:80          │
                                                         ▼ (internal only)
                              prometheus:9090 ◀── host-api:8080/metrics
                                                ◀── host-agentapi:8081/metrics
                                                ◀── worker-*:9102 (all four)
                                                ◀── rabbitmq:15692 (built-in plugin)
                                                ◀── node-exporter:9100
                                                ◀── postgres-exporter:9187
                              grafana ◀── prometheus:9090 (provisioned)
                              uptime-kuma ──polls──▶ https://$DOMAIN/health/ready
```

Only ports 80 and 443 are published. **Nothing else listens on the host**:
`/metrics`, Grafana (3000), Prometheus (9090), Uptime Kuma (3001),
RabbitMQ management (15672) are reachable *only* inside the compose network
or over an SSH tunnel:

```bash
ssh -L 3000:localhost:3000 -L 9090:localhost:9090 deploy@$HOST
# then open http://localhost:3000 (Grafana) and http://localhost:9090 (Prometheus)
```

To expose anything else publicly you must add an nginx server block *and*
justify it in the PR — the default posture is deny.

---

## 2. TLS lifecycle (Let's Encrypt)

| Step | Command | When |
|---|---|---|
| First issue | `DOMAIN=… EMAIL=… ./deploy/nginx/init-letsencrypt.sh` | once, before first `up` |
| Renew | host cron (see below) | weekly; certbot skips if >30d remain |
| Verify | `echo \| openssl s_client -connect $DOMAIN:443 -servername $DOMAIN 2>/dev/null \| openssl x509 -noout -dates` | after issue/renew |

Cron (host, as root) — backup and renewal side by side:

```cron
0 2 * * *  /opt/daraban/deploy/backup/backup.sh >>/var/log/daraban-backup.log 2>&1
0 3 * * 0  docker compose -f /opt/daraban/docker-compose.yml -f /opt/daraban/docker-compose.prod.yml run --rm certbot renew --quiet && docker compose -f /opt/daraban/docker-compose.yml -f /opt/daraban/docker-compose.prod.yml exec nginx nginx -s reload
```

Renewal gotchas:

- Port 80 must stay reachable — the `:80` server block serves
  `/.well-known/acme-challenge/` and redirects *everything else*. Do not add
  auth or IP restrictions to that location.
- `init-letsencrypt.sh` is **not idempotent by design**: it deletes the dummy
  cert (step 4). Re-running it after a real cert exists replaces it
  (`--force-renewal`) and hits Let's Encrypt rate limits if abused. For renewals
  use the cron line above, never the init script.
- Validate any nginx edit before reload: `docker compose exec nginx nginx -t`.

---

## 3. Backups

### 3.1 What, where, how many

`deploy/backup/backup.sh` runs nightly at 02:00 and produces, under
`$BACKUP_ROOT` (default `/var/backups/daraban`):

```
daily/   daraban-pg-YYYY-MM-DD.dump.gz.enc      (newest   7 kept)
         daraban-redis-YYYY-MM-DD.rdb.gz.enc
weekly/  …-Www.…                                (newest   4 kept, Sundays)
monthly/ daraban-pg-YYYY-MM.…                   (newest  12 kept, 1st of month)
```

Snapshots are `pg_dump -Fc` (Postgres, custom format: allows single-table
restore) and Redis `dump.rdb` (RDB enabled alongside AOF in the prod
overlay), each gzipped then AES-256-CBC encrypted (PBKDF2) with the passphrase
in `$BACKUP_PASSPHRASE_FILE`. Weekly/monthly tiers are hard links into the
same inode — one copy on disk, three retention clocks.

If `BACKUP_REMOTE` is set (`backup@store:/srv/daraban`), the pruned set is
rsynced off-server every run. **If it is unset the script says so loudly in
the log on every run** — a backup that has never left the building is a hope,
not a backup.

### 3.2 Retention arithmetic (why these numbers)

- 7 daily: a full week to notice silent corruption (bad deploy, slow data bug).
- 4 weekly (Sundays): a month of known-good starting points.
- 12 monthly (1st): a year for compliance/audit lookups.
- Total worst case ≈ 7 + 4 + 12 snapshots × 2 datasets; size-bounded by the
  database, not by time. Overrides: `KEEP_DAILY/WEEKLY/MONTHLY` env.

### 3.3 Verifying backups (do this monthly, not on incident day)

```bash
# 1. Pick the newest daily snapshot and decrypt to a temp dir (never in place):
f=$(ls -t /var/backups/daraban/daily/daraban-pg-*.dump.gz.enc | head -1)
openssl enc -d -aes-256-cbc -pbkdf2 -pass file:/root/.daraban-backup-pass \
  -in "$f" | gunzip > /tmp/restore-test.dump
# 2. Restore into a SCRATCH database (never the live one):
docker compose exec -T postgres psql -U daraban -c "CREATE DATABASE restore_test;"
cat /tmp/restore-test.dump | docker compose exec -T postgres pg_restore -U daraban -d restore_test
# 3. Sanity: table counts look plausible, then drop it:
docker compose exec -T postgres psql -U daraban -d restore_test -c "SELECT count(*) FROM identity.users;"
docker compose exec -T postgres psql -U daraban -c "DROP DATABASE restore_test;"
shred -u /tmp/restore-test.dump
```

If step 2 fails, the backup pipeline is broken — fix it before you need it.
Log the verification date wherever your team tracks ops tasks.

---

## 4. Restore procedures

> Stop writes first: `docker compose stop host-api host-agentapi
> worker-automation worker-notifications worker-reporting worker-reports`.
> Every app service holds Postgres connections (DbContext pools included), and
> DROP DATABASE fails while any are open -- so this is unconditional, not
> "if they write". Leave postgres/redis/rabbitmq/nginx running. Restoring
> under live writes guarantees a split-brain you will discover at 3 AM.

### 4.1 Full Postgres restore (disaster: database lost/corrupt)

```bash
cd /opt/daraban
# 1. Decrypt the chosen snapshot (prefer the newest verified daily):
f=/var/backups/daraban/daily/daraban-pg-2026-09-15.dump.gz.enc
openssl enc -d -aes-256-cbc -pbkdf2 -pass file:/root/.daraban-backup-pass \
  -in "$f" | gunzip > /tmp/restore.dump
# 2. Drop and recreate the database (DESTRUCTIVE -- confirm the snapshot date first):
docker compose exec -T postgres psql -U daraban -c "DROP DATABASE daraban_platform;"
docker compose exec -T postgres psql -U daraban -c "CREATE DATABASE daraban_platform;"
# 3. Restore (custom format; single-threaded because the dump arrives on stdin --
# pg_restore -j (parallel) only works against archive files, not pipes).
cat /tmp/restore.dump | docker compose exec -T postgres pg_restore -U daraban -d daraban_platform
# 4. Restart everything, watch the health gate:
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
docker compose ps   # all services (healthy)
shred -u /tmp/restore.dump
```

Expected downtime: minutes (restore speed ≈ disk speed; a 10 GB dump is
typically < 10 min). Rollback: the pre-restore state is gone once you DROP —
take a fresh `pg_dump` of the *broken* database first if there is any chance
its newer rows matter (`... > /tmp/broken-pre-restore.dump` before step 2).

### 4.2 Single-table restore (oops-delete, not disaster)

`pg_restore -t <table>` against a scratch DB, then copy the rows across:

```bash
# list tables in the snapshot: pg_restore -l /tmp/restore.dump | grep TABLE
cat /tmp/restore.dump | docker compose exec -T postgres pg_restore -U daraban -d restore_test -t servicedesk.tickets
```

### 4.3 Redis restore (cache/session loss)

Redis holds cache + permission-cache + SignalR backplane state — all
**reconstructible**, so Redis restore is rarely needed (a cold restart
repopulates from Postgres). If you must:

```bash
docker compose stop host-api host-agentapi
# decrypt + place as dump.rdb, then restart redis (it loads RDB on boot):
openssl enc -d -aes-256-cbc -pbkdf2 -pass file:/root/.daraban-backup-pass \
  -in /var/backups/daraban/daily/daraban-redis-2026-09-15.rdb.gz.enc \
  | gunzip > /tmp/dump.rdb
docker compose cp /tmp/dump.rdb redis:/data/dump.rdb
docker compose up -d redis && sleep 5 && docker compose up -d
shred -u /tmp/dump.rdb
```

Prefer `FLUSHDB`-and-rewarm (just restart redis empty) unless you have a
reason to believe the cached state itself is the asset — it never is.

### 4.4 Full-server rebuild (new machine)

1. Provision Ubuntu 24.04, Docker Engine + Compose plugin, DNS → new IP.
2. Copy `/opt/daraban` (git checkout the deployed tag + `.env` + `certs/`
   JWT key + `deploy/nginx/certs/` if reusing the domain).
3. `init-letsencrypt.sh` (fresh cert on the new host).
4. Restore Postgres per §4.1 from the off-server copy (`BACKUP_REMOTE`).
5. `docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d`.
6. Verify §8 checklist.

---

## 5. Monitoring & alerting

| Surface | Reach it | What it tells you |
|---|---|---|
| Grafana | `http://localhost:3000` over SSH tunnel | Service Health dashboard (request rate, error rate, queue depth, memory, disk) |
| Prometheus | `http://localhost:9090` over SSH tunnel | `/graph` for ad-hoc PromQL, `/alerts` for firing rules |
| Uptime Kuma | `http://localhost:3001` over SSH tunnel | Black-box HTTP checks with history + flap detection |

First-run setup (once per install):

1. Grafana login: `GRAFANA_ADMIN_USER` / `GRAFANA_ADMIN_PASSWORD` from `.env`
   (change it in the UI immediately; afterwards the env values are ignored).
   The Prometheus datasource and the dashboard are provisioned from git — do
   not edit them in the UI (provisioned dashboards reset on restart by design;
   edit `deploy/monitoring/grafana/dashboards/*.json` and redeploy instead).
2. Uptime Kuma: create the admin account on first visit, then add monitors:
   `https://$DOMAIN/health/ready` (keyword `Healthy`), plus each host's
   `/health/live`. Enable a notification channel (Telegram/SMTP) there.
3. Prometheus `/targets`: all 11 scrape targets Up (2 hosts + 4 workers +
   rabbitmq + node + postgres-exporter). A Down target means the exporter or
   the service is gone — check `docker compose ps` first.

### 5.1 Alerts (rules.yml — evaluated inside Prometheus)

| Alert | Fires when | First response |
|---|---|---|
| `DarabanErrorSpike` (critical) | >5% 5xx per service over 5m | Grafana error panel → which endpoint → `docker compose logs host-api` |
| `DarabanTargetDown` (critical) | any scrape target unreachable 2m | `docker compose ps`; if a service is down, other alerts for it are suspect |
| `DarabanQueueBackup` (warning) | >1000 ready messages for 10m | worker logs; RabbitMQ UI via tunnel (`:15672`) |
| `DarabanDiskSpaceLow` (critical) | root <15% free for 5m | prune backups/Docker (`docker system prune`), grow volume |
| `DarabanMemoryPressure` (warning) | >90% for 10m | Grafana memory panel: which service; check Postgres buffers |

There is deliberately no Alertmanager/SMTP in this stack — firing alerts are
visible in Prometheus/Grafana/Uptime Kuma, and Uptime Kuma's own notification
channels cover paging. Wiring Alertmanager is a documented follow-up.

### 5.2 Metric-name grounding

PromQL in dashboards/rules assumes `http_server_requests_received_total{code,
method}` (prometheus-net), `rabbitmq_queue_messages_ready` (built-in plugin),
`node_filesystem_*`/`node_memory_*` (node-exporter), `process_working_set_bytes`
(dotnet runtime). Client upgrades rename series without asking: after any
`prometheus-net` or exporter bump, open Prometheus `/graph`, confirm each name
still exists, and fix the queries *before* the next deploy, not during an
incident.

---

## 6. Log access

```bash
# One service, last 200 lines:
docker compose logs --tail=200 host-api
# Follow everything during an incident:
docker compose -f docker-compose.yml -f docker-compose.prod.yml logs -f --tail=100
# Serilog rolling files also exist inside containers; the compose tail above is
# sufficient for every procedure in this runbook.
```

---

## 7. Rollback (bad deploy)

CD keeps the previous image tags; a rollback is a re-deploy of the last good
tag, not a code revert under fire:

```bash
cd /opt/daraban
# DARABAN_TAG currently points at the bad release; flip it back:
sed -i 's/^DARABAN_TAG=.*/DARABAN_TAG=<last-good-sha>/' .env
docker compose -f docker-compose.yml -f docker-compose.prod.yml pull host-api host-agentapi frontend
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
docker compose ps   # wait for (healthy), then verify /health/ready in §8
```

`cd.yml` already health-gates deploys and auto-rolls-back on failure
(docs/09); this section is the manual equivalent when the gate itself is
what you are working around.

---

## 8. First-deploy / post-change verification checklist

Run after every production change, not just the first install:

- [ ] `docker compose -f docker-compose.yml -f docker-compose.prod.yml config` renders with no errors
- [ ] `docker compose ps` — all services `Up`, infra `(healthy)`
- [ ] `https://$DOMAIN/health/ready` returns Healthy JSON (through nginx, not direct)
- [ ] `http://$DOMAIN` redirects (301) to `https://$DOMAIN`
- [ ] Login works in the UI (exercises auth + JWT + edge routing end to end)
- [ ] `curl -sk https://$DOMAIN/metrics` returns **404** (edge must not expose it)
- [ ] Prometheus `/targets`: 11/11 Up
- [ ] Grafana dashboard shows all six panels with data
- [ ] Uptime Kuma monitors green
- [ ] `backup.sh --dry-run` prints the expected plan; last real run in the log
- [ ] TLS expiry > 60 days (`openssl s_client` check in §2)
