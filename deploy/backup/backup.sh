#!/usr/bin/env bash
# =============================================================================
# backup.sh — encrypted grandfather-father-son backups for Daraban Platform (Task 8.4).
#
# What gets backed up:
#   1. PostgreSQL  -- pg_dump custom format (-Fc) of $POSTGRES_DB via the running
#                      container, then gzip, then AES-256-CBC.
#   2. Redis       -- the RDB snapshot (dump.rdb) copied out of the redis container
#                      (RDB is enabled by the prod overlay's --save rules alongside AOF),
#                      then gzip, then AES-256-CBC.
#
# Layout (all under $BACKUP_ROOT, default /var/backups/daraban):
#   daily/   daraban-pg-YYYY-MM-DD.dump.gz.enc      (kept  7)
#   weekly/  daraban-pg-YYYY-Www.dump.gz.enc        (kept  4, Sunday snapshot)
#   monthly/ daraban-pg-YYYY-MM.dump.gz.enc         (kept 12, 1st-of-month snapshot)
#   ... same three tiers for daraban-redis-*.rdb.gz.enc
# A snapshot always lands in daily/; on Sundays it is ALSO hard-linked into
# weekly/, on the 1st ALSO into monthly/. One file on disk, three retention
# clocks -- pruning is then a plain per-directory mtime/count trim.
#
# Encryption: openssl AES-256-CBC with PBKDF2 (-pbkdf2) and a passphrase read
# from $BACKUP_PASSPHRASE_FILE (a root-only file on the host, NOT in git, NOT in
# .env which docker compose would expose to every container's environment).
#
# Off-server copy: if BACKUP_REMOTE is set (rsync destination, e.g.
# "backup@store:/srv/daraban"), the pruned set is synced after every run. The
# remote must accept the host's SSH key non-interactively.
#
# Usage:
#   BACKUP_PASSPHRASE_FILE=/root/.daraban-backup-pass ./deploy/backup/backup.sh
#   ./deploy/backup/backup.sh --dry-run   # prints what WOULD run, touches nothing
#
# Cron (host, as root):  0 2 * * *  /opt/daraban/deploy/backup/backup.sh >>/var/log/daraban-backup.log 2>&1
# Certbot renewal beside it:  0 3 * * 0  docker compose -f /opt/daraban/docker-compose.yml \
#   -f /opt/daraban/docker-compose.prod.yml run --rm certbot renew --quiet \
#   && docker compose -f /opt/daraban/docker-compose.yml \
#   -f /opt/daraban/docker-compose.prod.yml exec nginx nginx -s reload
# =============================================================================
set -euo pipefail

# Private by default: the plaintext dump exists briefly between pg_dump and the
# rm after encryption. Without this it would inherit the caller's umask (often
# 022 -- world-readable database contents for a few seconds every night).
umask 077

# Backticks are banned in ${VAR:?...} messages: inside double quotes they would
# parse as command substitution and break the script (caught by bash -n).
DRY_RUN=0
if [[ "${1:-}" == "--dry-run" ]]; then
  DRY_RUN=1
fi

COMPOSE_DIR="${COMPOSE_DIR:-/opt/daraban}"
BACKUP_ROOT="${BACKUP_ROOT:-/var/backups/daraban}"
KEEP_DAILY="${KEEP_DAILY:-7}"
KEEP_WEEKLY="${KEEP_WEEKLY:-4}"
KEEP_MONTHLY="${KEEP_MONTHLY:-12}"
BACKUP_REMOTE="${BACKUP_REMOTE:-}"
# Required for real runs only: --dry-run must work without any secrets present so the
# control flow (tiers, prune math, remote sync line) is testable anywhere.
if [[ "${1:-}" != "--dry-run" && -z "${BACKUP_PASSPHRASE_FILE:-}" ]]; then
  echo "Set BACKUP_PASSPHRASE_FILE to a root-only file holding the backup passphrase" >&2
  exit 1
fi

POSTGRES_USER="${POSTGRES_USER:-daraban}"
POSTGRES_DB="${POSTGRES_DB:-daraban_platform}"

log() { echo "[$(date -u +%FT%TZ)] $*"; }
run() { if [[ "$DRY_RUN" == "1" ]]; then echo "  [dry-run] $*"; else eval "$@"; fi; }

dc() { docker compose -f "$COMPOSE_DIR/docker-compose.yml" -f "$COMPOSE_DIR/docker-compose.prod.yml" "$@"; }

TODAY="$(date -u +%F)"
DOW="$(date -u +%u)"     # 1=Mon .. 7=Sun
DOM="$(date -u +%d)"
WEEK="$(date -u +%V)"

log "backup starting (dry-run=$DRY_RUN) root=$BACKUP_ROOT"
run mkdir -p "$BACKUP_ROOT/daily" "$BACKUP_ROOT/weekly" "$BACKUP_ROOT/monthly"

# ---- 1. PostgreSQL -----------------------------------------------------------
PG_FILE="daraban-pg-$TODAY.dump"
log "dumping postgres ($POSTGRES_DB) ..."
if [[ "$DRY_RUN" == "1" ]]; then
  echo "  [dry-run] dc exec -T postgres pg_dump -U $POSTGRES_USER -Fc $POSTGRES_DB > daily/$PG_FILE"
else
  dc exec -T postgres pg_dump -U "$POSTGRES_USER" -Fc "$POSTGRES_DB" > "$BACKUP_ROOT/daily/$PG_FILE"
fi

# ---- 2. Redis ----------------------------------------------------------------
# Trigger a background save so dump.rdb reflects this minute, then copy it out.
RDB_FILE="daraban-redis-$TODAY.rdb"
log "snapshotting redis ..."
if [[ "$DRY_RUN" == "1" ]]; then
  echo "  [dry-run] dc exec -T redis redis-cli BGSAVE + dc cp redis:/data/dump.rdb daily/$RDB_FILE"
else
  dc exec -T redis redis-cli BGSAVE >/dev/null
  # BGSAVE is async: wait for the background save to finish (rdb_bgsave_in_progress:0).
  saved=0
  for _ in $(seq 1 30); do
    if dc exec -T redis redis-cli INFO persistence | grep -q "rdb_bgsave_in_progress:0"; then
      saved=1
      break
    fi
    sleep 2
  done
  if [[ "$saved" == "0" ]]; then
    # Loud, not fatal: a stale RDB plus a warning beats no backup at all, but the
    # operator must see this in the cron log -- a perpetually-timing-out BGSAVE
    # means Redis is misconfigured or the disk is stalled.
    log "WARNING: redis BGSAVE did not finish in 60s -- copying the last completed dump.rdb instead" >&2
  fi
  # Service name (not the container_name): survives a container rename.
  dc cp redis:/data/dump.rdb "$BACKUP_ROOT/daily/$RDB_FILE"
fi

# ---- 3. Compress + encrypt ----------------------------------------------------
encrypt_one() {
  local plain="$1"
  local enc="$plain.gz.enc"
  log "encrypting $(basename "$plain") ..."
  if [[ "$DRY_RUN" == "1" ]]; then
    echo "  [dry-run] gzip + openssl enc -aes-256-cbc -pbkdf2 -pass file:\$BACKUP_PASSPHRASE_FILE"
    return
  fi
  gzip -c "$plain" | openssl enc -aes-256-cbc -pbkdf2 \
    -pass "file:$BACKUP_PASSPHRASE_FILE" -out "$enc"
  rm -f "$plain"
  chmod 600 "$enc"
}

if [[ "$DRY_RUN" == "0" ]]; then
  encrypt_one "$BACKUP_ROOT/daily/$PG_FILE"
  encrypt_one "$BACKUP_ROOT/daily/$RDB_FILE"
else
  echo "  [dry-run] encrypt daily/$PG_FILE + daily/$RDB_FILE"
fi

# ---- 4. Weekly / monthly tiers (hard links: one inode, three clocks) ----------
if [[ "$DRY_RUN" == "1" ]]; then
  echo "  [dry-run] tier linking (dow=$DOW dom=$DOM)"
else
  if [[ "$DOW" == "7" ]]; then
    ln -f "$BACKUP_ROOT/daily/$PG_FILE.gz.enc"  "$BACKUP_ROOT/weekly/daraban-pg-$TODAY-W$WEEK.dump.gz.enc"
    ln -f "$BACKUP_ROOT/daily/$RDB_FILE.gz.enc" "$BACKUP_ROOT/weekly/daraban-redis-$TODAY-W$WEEK.rdb.gz.enc"
    log "weekly tier linked"
  fi
  if [[ "$DOM" == "01" ]]; then
    ln -f "$BACKUP_ROOT/daily/$PG_FILE.gz.enc"  "$BACKUP_ROOT/monthly/daraban-pg-${TODAY:0:7}.dump.gz.enc"
    ln -f "$BACKUP_ROOT/daily/$RDB_FILE.gz.enc" "$BACKUP_ROOT/monthly/daraban-redis-${TODAY:0:7}.rdb.gz.enc"
    log "monthly tier linked"
  fi
fi

# ---- 5. Retention prune (newest KEEP_* files per tier survive) -----------------
prune() {
  local dir="$1" keep="$2" pattern="$3"
  log "pruning $dir to newest $keep ($pattern) ..."
  if [[ "$DRY_RUN" == "1" ]]; then echo "  [dry-run] prune"; return; fi
  # ls -t: newest first; tail drops the survivors; xargs -r deletes only the rest.
  # Filenames are machine-generated (no spaces/newlines), so parsing ls is safe here.
  ls -t "$dir"/$pattern 2>/dev/null | tail -n +"$((keep + 1))" | xargs -r rm -f --
}

prune "$BACKUP_ROOT/daily"   "$KEEP_DAILY"   "*.enc"
prune "$BACKUP_ROOT/weekly"  "$KEEP_WEEKLY"  "*.enc"
prune "$BACKUP_ROOT/monthly" "$KEEP_MONTHLY" "*.enc"

# ---- 6. Off-server copy --------------------------------------------------------
if [[ -n "$BACKUP_REMOTE" ]]; then
  log "syncing to $BACKUP_REMOTE ..."
  if [[ "$DRY_RUN" == "1" ]]; then
    echo "  [dry-run] rsync -az --delete $BACKUP_ROOT/ $BACKUP_REMOTE/"
  else
    rsync -az --delete "$BACKUP_ROOT/" "$BACKUP_REMOTE/"
  fi
else
  log "BACKUP_REMOTE unset -- backups stay on this host only (set it; a backup that has never left the building is a hope, not a backup)"
fi

# Counts retained snapshots without failing when a tier is empty (fresh installs
# have no weekly/monthly yet). A bare `ls tier/*.enc | wc -l` would exit non-zero
# on the empty glob and, under `set -euo pipefail`, kill an otherwise successful
# run -- reporting failure every night until the first Sunday.
count_snapshots() {
  local total=0 tier
  for tier in daily weekly monthly; do
    if [[ -d "$BACKUP_ROOT/$tier" ]]; then
      total=$((total + $(find "$BACKUP_ROOT/$tier" -maxdepth 1 -name '*.enc' | wc -l)))
    fi
  done
  echo "$total"
}

log "backup complete: $(count_snapshots) encrypted snapshots retained"
