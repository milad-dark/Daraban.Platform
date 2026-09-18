# Deployment Guide — everything, where to set it, and how to go live

Complete, self-contained runbook for deploying Daraban Platform with this
repository's CI/CD. Covers **every variable**, **exactly where it is set**
(GitHub UI vs. server file), and both operating modes:

| Mode | You have… | Result of a push to `master` |
|---|---|---|
| **A — GitHub-only (publish)** | no server yet | CI passes → all 6 images published to Docker Hub → `deploy` job **skipped** (grey) |
| **B — Full (publish + deploy)** | a Linux server with Docker | images published → CD SSHes in, rolling update with health gate, auto-rollback on failure |

> **Prerequisite for the "deploy job skips cleanly" behaviour:** merge PR #37
> (the `DEPLOY_ENABLED` gate in `cd.yml`). Until it is on `master`, the deploy
> job runs unconditionally and fails red when `DEPLOY_*` secrets are missing.

---

## 1. Where each thing lives (mental model)

```
GitHub cloud                          Your server (/opt/daraban)
┌─────────────────────────────┐       ┌──────────────────────────────┐
│ Settings → Secrets and      │       │ .env  ← NOT in git           │
│   variables → Actions       │       │   DOCKERHUB_USERNAME,        │
│  Secrets:  DOCKERHUB_*      │──SSH──▶   DARABAN_TAG (CD flips this),│
│            DEPLOY_*         │       │   DB/Rabbit/JWT passwords,   │
│  Variables: DEPLOY_ENABLED  │       │   NGINX_PORT ...             │
│                             │       │ certs/jwt-signing-key.pem    │
│ Docker Hub ←── CD pushes    │       │ docker-compose.yml (from git)│
└─────────────────────────────┘       └──────────────────────────────┘
```

Three distinct places — never mix them up:

1. **GitHub *Secrets*** — redacted, for credentials. Path:
   repo → **Settings** → **Secrets and variables** → **Actions** → tab
   **Secrets** → *New repository secret*. CLI: `gh secret set NAME`.
2. **GitHub *Variables*** — visible, for flags/config. Same page → tab
   **Variables**. CLI: `gh variable set NAME --body value`.
3. **Server `.env`** (`/opt/daraban/.env`) — runtime config read by
   `docker compose`. Created from `.env.example`; never committed.

---

## 2. Reference: every GitHub secret

Repo → Settings → Secrets and variables → Actions → **Secrets**.

| Name | Mode A | Mode B | What it is | How to create it |
|---|---|---|---|---|
| `DOCKERHUB_USERNAME` | ✅ required | ✅ required | Your Docker Hub username; becomes the image namespace `{username}/daraban-*` | Sign in at hub.docker.com → your profile |
| `DOCKERHUB_TOKEN` | ✅ required | ✅ required | Docker Hub **access token** (NOT your account password) | hub.docker.com → Account Settings → **Security** → *New Access Token* → type **Read, Write** → copy the token once |
| `DEPLOY_HOST` | ⬜ | ✅ required | Server address (IP or hostname) reachable from GitHub | `ip addr` / your VPS panel |
| `DEPLOY_USER` | ⬜ | ✅ required | SSH user on the server; must own `/opt/daraban/.env` (CD rewrites `DARABAN_TAG` in it) and be in the `docker` group | e.g. `ubuntu`, `deploy` |
| `DEPLOY_SSH_KEY` | ⬜ | ✅ required | **Private** key of a dedicated deploy keypair | `ssh-keygen -t ed25519 -f daraban_deploy -C "github-cd" -N ""` → set this secret to the **private** key file contents; append the `.pub` file to `~/.ssh/authorized_keys` of `DEPLOY_USER` on the server |
| `DEPLOY_PATH` | ⬜ | optional | Checkout dir on server; default `/opt/daraban` | only set if you use another path |

CLI equivalents (run in a terminal inside the repo):

```bash
gh secret set DOCKERHUB_USERNAME   # then paste value
gh secret set DOCKERHUB_TOKEN
gh secret set DEPLOY_HOST
gh secret set DEPLOY_USER
gh secret set DEPLOY_SSH_KEY < daraban_deploy   # pipe the private key file
```

Verify: `gh secret list`.

## 3. Reference: every GitHub variable

Repo → Settings → Secrets and variables → Actions → **Variables**.

| Name | Default | Meaning |
|---|---|---|
| `DEPLOY_ENABLED` | **absent = deploy skipped** | Set to `true` **only** once a real server + all `DEPLOY_*` secrets exist. Why a *variable*: GitHub job-level `if:` cannot read secrets, only vars. |

```bash
gh variable set DEPLOY_ENABLED --body true    # enable server deploys
gh variable set DEPLOY_ENABLED --body false   # disable again (safe kill-switch)
gh variable list
```

## 4. Reference: every server `.env` key (`/opt/daraban/.env`)

Copy the repo's `.env.example` as a base, then set **production values**
for these (this is the complete list `docker-compose.yml` consumes):

### 4.1 Image provenance (CD depends on these two)
| Key | Example | Notes |
|---|---|---|
| `DOCKERHUB_USERNAME` | `milad-dark` | Same value as the GitHub secret — compose resolves `${DOCKERHUB_USERNAME}/daraban-api:${DARABAN_TAG}` |
| `DARABAN_TAG` | `latest` or a git SHA | **CD rewrites this line on every deploy** — `DEPLOY_USER` must be able to write the file |

### 4.2 Infrastructure credentials
| Key | Example / rule |
|---|---|
| `POSTGRES_USER` | `daraban` |
| `POSTGRES_PASSWORD` | strong unique secret (this is what the *postgres container* uses) |
| `POSTGRES_DB` | `daraban_platform` |
| `RABBITMQ_DEFAULT_USER` | `daraban` |
| `RABBITMQ_DEFAULT_PASS` | strong unique secret |
| `CONNECTIONSTRINGS__Postgres` | `Host=postgres;Port=5432;Database=daraban_platform;Username=daraban;Password=<same as POSTGRES_PASSWORD>` |
| `CONNECTIONSTRINGS__Redis` | `redis:6379` |
| `RABBITMQ__Host` / `__Port` / `__Username` / `__Password` | `rabbitmq` / `5672` / as above / same as `RABBITMQ_DEFAULT_PASS` |

### 4.3 App / auth
| Key | Example / rule |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` (⚠ never `Development` — see 4.4) |
| `JWT__SigningKeyPemPath` | `/app/certs/jwt-signing-key.pem` (container path; file lives at `./certs/` on the server, mounted read-only) |
| `JWT__Issuer` / `JWT__Audience` | e.g. `https://your.domain` / `daraban-api` |
| `JWT__AccessTokenExpirationMinutes` / `JWT__RefreshTokenExpirationDays` | defaults `15` / `7` |
| `NGINX_PORT` | `80` — the **only** published app port; everything else is internal |
| `SERILOG__MinimumLEVEL__DEFAULT` | `Information` |

### 4.4 JWT signing key — required in Production
`JwtSigningKeyProvider` **refuses to start** outside Development without a
persistent RSA key (an ephemeral key would invalidate every token on restart):

```bash
mkdir -p /opt/daraban/certs
openssl genrsa -out /opt/daraban/certs/jwt-signing-key.pem 3072
chmod 600 /opt/daraban/certs/jwt-signing-key.pem
```
Keep it out of git (`.dockerignore`/`.gitignore` already exclude `certs/` and `.env*`).
All hosts (user API + agent API) must share the **same** key.

---

## 5. Mode A — "publish on my GitHub, no server" (do this today)

1. Set the two required secrets (§2): `DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN`.
2. Do **not** set `DEPLOY_ENABLED` (or set it to `false`).
3. Make sure PR #37 is merged so the gate is on `master`.
4. Merge/push anything to `master` → watch it: `gh run list --workflow=CD --limit 3`
5. Result: 6 images live on Docker Hub:
   ```
   {username}/daraban-api:<sha|latest>
   {username}/daraban-agentapi:<sha|latest>
   {username}/daraban-frontend:<sha|latest>
   {username}/daraban-worker-automation:<sha|latest>
   {username}/daraban-worker-notifications:<sha|latest>
   {username}/daraban-worker-reporting:<sha|latest>
   ```
   Deploy job shows **skipped** — pipeline is green, not red.

**Run the published stack anywhere without GitHub** (laptop, home NAS, any
Docker host) — this is also how you smoke-test before buying a server:

```bash
git clone https://github.com/milad-dark/Daraban.Platform && cd Daraban.Platform
cp .env.example .env      # edit: real passwords, DOCKERHUB_USERNAME=<you>, DARABAN_TAG=latest
mkdir -p certs && openssl genrsa -out certs/jwt-signing-key.pem 3072
docker login
docker compose pull && docker compose up -d
curl http://localhost/          # nginx gateway → frontend
curl http://localhost/health/live
```
> ⚠ Same caveat as §6.1 step 8: schema is not bootstrapped automatically;
> the unified migration set is a documented pending item (`docs/06` §5).

## 6. Mode B — deploy to a real server

### 6.1 One-time server setup
1. Ubuntu/Debian with Docker Engine + compose plugin:
   `curl -fsSL https://get.docker.com | sh`
2. `usermod -aG docker $DEPLOY_USER`
3. Clone the repo to `/opt/daraban` (compose file + `docker/` + nginx config must exist there; CD only ever changes `DARABAN_TAG`).
4. Create `/opt/daraban/.env` per §4 and the JWT key per §4.4.
5. Install the deploy **public** key: `cat daraban_deploy.pub >> ~/.ssh/authorized_keys`
6. `docker login` once on the server (credentials persist in `~/.docker/config.json`).
7. First boot: `cd /opt/daraban && docker compose pull && docker compose up -d`
8. Apply the database schema **manually** — the pipeline intentionally does
   not run migrations, and the unified EF migration set is still pending
   (`docs/06-Not-Implemented.md` §5): run `dotnet ef database update` per
   module DbContext that has migrations (currently: Knowledge). The composite
   indexes from Task 8.2 are EF-configuration-only; apply them with
   `CREATE INDEX CONCURRENTLY …` (commands in `docs/07-Performance.md` §2).
9. Smoke-test: gateway up, `/health/live` 200, login works, workers `docker compose ps` all healthy.

### 6.2 One-time GitHub setup
Set `DEPLOY_HOST`, `DEPLOY_USER`, `DEPLOY_SSH_KEY` (+ `DEPLOY_PATH` if not
`/opt/daraban`) — then flip the switch:
```bash
gh variable set DEPLOY_ENABLED --body true
```

### 6.3 Steady state
Every merge to `master` now: CI → build+push 6 images (`:<sha>` + `:latest`) →
SSH deploy:
1. remembers current `DARABAN_TAG` as rollback point,
2. pins new SHA, `compose pull`,
3. rolling update **health-gated per service** (`host-api` healthy → `host-agentapi` → workers → frontend → gateway probe, ≤2 min each),
4. any failure → redeploys previous tag and exits red.

Manual rollback at any time (or if you prefer zero GitHub involvement):
edit `DARABAN_TAG` in the server `.env` to any past SHA and
`docker compose up -d --no-deps` — old images stay on Docker Hub forever.
Manual CD run without a new commit: **Actions → CD → Run workflow**.

### 6.4 Production overlay (Task 8.4 — TLS, monitoring, backups)

The base `docker-compose.yml` is dev-friendly (HTTP only, no observability).
Production runs it **plus** `docker-compose.prod.yml`:

```bash
cd /opt/daraban && docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

What the overlay adds: nginx on `:80/:443` with `deploy/nginx/nginx.prod.conf`
(TLS 1.2+, HTTP→HTTPS redirect, auth rate limits, gzip), Prometheus +
Grafana + node/postgres exporters + Uptime Kuma (internal network only — SSH
tunnels per `docs/10-Backup-Restore-Runbook.md` §1), Redis RDB snapshots
alongside AOF, and the RabbitMQ Prometheus plugin. Third-party image pins in
the overlay were verified on Docker Hub — bump deliberately, never `:latest`.

TLS bootstrap (once, before first `up`):

```bash
DOMAIN=daraban.example.com EMAIL=ops@example.com ./deploy/nginx/init-letsencrypt.sh
```

This mints a dummy cert so nginx boots, starts it, replaces the dummy with a
real HTTP-01 certificate, and reloads. Renewals run from host cron (weekly
`certbot renew` + nginx reload) — never re-run the init script (it uses
`--force-renewal` and burns rate limit). Verify: `nginx -t` inside the
container after any conf edit; `openssl s_client` dates after issue/renew.

First production boot order: base `up -d` (or overlay directly) → schema per
§6.1 step 8 → TLS init → overlay `up -d` → run the verification checklist in
`docs/10-Backup-Restore-Runbook.md` §8 (edge redirect, login, `/metrics`
returns 404 publicly, 11/11 Prometheus targets Up, backup `--dry-run`).

Backups start working the first night automatically once
`BACKUP_PASSPHRASE_FILE` exists (cron line + passphrase setup in
`docs/10-Backup-Restore-Runbook.md` §3); without it the script fails closed
and logs exactly what's missing.

## 7. Troubleshooting (errors you may actually see, all previously hit)

| Symptom | Cause | Fix |
|---|---|---|
| CD login step: `Error: Username and password required` | `DOCKERHUB_USERNAME`/`DOCKERHUB_TOKEN` secrets missing | §2 steps 1 |
| deploy: `error: missing server host` | `DEPLOY_*` secrets unset and gate not merged / `DEPLOY_ENABLED=true` set too early | set them, or `gh variable set DEPLOY_ENABLED --body false` |
| `Unexpected input(s) 'script_stop'` | stale workflow version | removed in PR #37; use current `cd.yml` |
| host-api restarts with signing-key exception in logs | Production without `JWT__SigningKeyPemPath`/file | §4.4 |
| login works then breaks after restart | ephemeral dev key (`ASPNETCORE_ENVIRONMENT=Development` left on server) | set `Production` + real key |
| deploy fails "Health gate" | app unhealthy | SSH in: `docker compose ps`, `docker compose logs --tail=100 host-api` — rollback already happened automatically |
| `docker compose pull` unauthorized on server | never `docker login`ed on that machine | step 6.1-6 |

## 8. Quick checklist

- [ ] GitHub secret `DOCKERHUB_USERNAME` (§2)
- [ ] GitHub secret `DOCKERHUB_TOKEN` — access token, Read/Write (§2)
- [ ] PR #37 merged (deploy gate on master)
- [ ] Mode A: nothing else — push to master, verify `gh run list`
- [ ] (Mode B) server: repo cloned to `/opt/daraban`, `.env` per §4, JWT key per §4.4, docker login, public key in `authorized_keys`
- [ ] (Mode B) secrets `DEPLOY_HOST/USER/SSH_KEY`, then `gh variable set DEPLOY_ENABLED --body true`
- [ ] (Mode B) migrations applied manually once

*Related: `docs/08-CICD.md` (pipeline internals, image matrix), `.env.example`,
`docker-compose.yml`.*
