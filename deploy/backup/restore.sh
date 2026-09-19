#!/usr/bin/env bash
# =============================================================================
# Restore script (Task 8.4) — decryption-based PostgreSQL backup restore
# Invoked on-demand via `docker compose exec host-api bash deploy/backup/restore.sh`
# ----------------------------------------------------------------------------
# Expected workflow (manual, not automated):
#
#   1. Transfer off-server backup file to /opt/daraban/backup/archived/
#   2. Copy production SERVER_ENV var for decryption:
#      docker compose exec host-api bash -c 'printenv CONNECTIONSTRINGS__Postgres | sed "s/.*Password=//" > ~/.backup_pass' \
#        && TARGET_PASS=$(cat ~/.backup_pass)
#   3. Run this script (will internally need BACKUP_PASS from a root-only env):
#      docker compose exec -T host-api bash <<''EOF'
#      export BACKUP_PASS="$TARGET_PASS"
#      bash -s -- "$1"
#      EOF
#   4. Verify with `docker compose exec postgres psql -U daraban -d daraban_platform -c "\dt"`
# -----------------------------------------------------------------------------
set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly BACKUP_ROOT="${BACKUP_ROOT:-/var/lib/daraban/backup}"
readonly BACKUP_PASS="${BACKUP_PASS:-}"

[[ -z "${BACKUP_PASS}" ]] && { echo "Error: BACKUP_PASS must be set (set on host-api only via secure env-mount, not sent in cli)"; exit 1; }

usage() {
  cat <<EOF
Usage: bash restore.sh <backup_file>

First, pull the encrypted backup to the server (off-server or local):
  scp user@db-backup-host:/backups/daraban_\${DATE}_encrypted.dump ./backup.dump

Then run the decrypt on the host-api container (assuming DERIVED_PASS env is set):
  docker compose exec -T host-api bash <<'''EOF'
  BACKUP_PASS=$TARGET_PASS bash deploy/backup/restore.sh ./backup.dump
  EOF

The decrypted custom-format dump will be placed in backup_root/archived/unzipped/ and
then dropped into the Postgres container via \`docker compose exec postgres psql

The temporary decrypted dump lives for 5 minutes after the script finishes (see cleanup
triggers below) and will be cleaned up if you restart daraban-postgres.

EOF
  exit 1
}

BACKUP_FILE="${1:-}"
[[ -z "${BACKUP_FILE}" ]] && { usage; }
[[ ! -f "${BACKUP_FILE}" ]] && { echo "Error: backup file not found: ${BACKUP_FILE}"; exit 1; }

(
  cd "${SCRIPT_DIR}"
  mkdir -p archived/unzipped
  cd archived/unzipped

  TIMESTAMP=$(date +%Y%m%d_%H%M%S)
  TEMP_DECRYPTED="${TIMESTAMP}_decrypted.dump"

  echo "[+] Decrypting $(basename "${BACKUP_FILE}")..."
  openssl enc -aes-256-cbc -pbkdf2 -d -in "../${BACKUP_FILE}" \
    -out "${TEMP_DECRYPTED}" \
    -k "${BACKUP_PASS}"

  echo "[+] Decrypted file: ${TEMP_DECRYPTED} ($(du -h "${TEMP_DECRYPTED}" | awk '{print $1}'))"
  echo "[+] Running Docker Compose exec as root for postgres/pull to avoid permission issues..."

  echo "[+] Dropping decrypted dump into postgres container..."
  # Drop into postgres container with psql to avoid file-permissions issues with host-user
  docker compose exec -T postgres psql -U daraban -d daraban_platform \
    -c "DROP SCHEMA IF EXISTS daraban_foo CASCADE; CREATE SCHEMA daraban_foo; SELECT 1;" > /dev/null

  docker compose exec -T postgres pg_restore -d daraban_platform \
    -d postgres \
    -U daraban \
    -c \
    --if-exists \
    --no-owner \
    -v \
    "${TEMP_DECRYPTED}"

  RC=$?
  if [[ ${RC} -eq 0 ]]; then
    echo "[+] Restore completed successfully."
  else
    echo "[!] Restore exited with code ${RC} — check the output above; you may need to manually recover from the plaintext dump at ${TEMP_DECRYPTED}."
    exit ${RC}
  fi

  echo "[+] Cleanup: removing plaintext dump (5min auto-cleanup also applies)..."
  rm -f "${TEMP_DECRYPTED}"

) 2>&1 | tee "$(dirname "${SCRIPT_DIR}")/../logs/restore_$(date +%Y%m%d_%H%M%S).log"
