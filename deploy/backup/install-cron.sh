#!/usr/bin/env bash
# =============================================================================
# Cron install script (Task 8.4) — installs and configures daily backup cron
# ----------------------------------------------------------------------------
# Run once on the server after backing up script is in /opt/daraban/backup/
# (same dir as docker-compose.yml, per Daraban.Platform conventions).
#
# Example usage on server:
#   cd /opt/daraban
#   bash deploy/backup/install-cron.sh
# -----------------------------------------------------------------------------
set -euo pipefail

readonly CRON_JOB_DIR="${CRON_JOB_DIR:-/var/spool/cron/crontabs}"
readonly BACKUP_ROOT="${BACKUP_ROOT:-/var/lib/daraban/backup}"
readonly DEPLOY_USER="${DEPLOY_USER:-ubuntu}"  # or whatever your deploy user is
readonly DEPLOY_SHELL="${DEPLOY_SHELL:-/bin/bash}"

verify_ssh() {
  local ssh_user="${1}"
  if ! ssh -o ConnectTimeout=3 -o BatchMode=yes "${ssh_user}@127.0.0.1" "echo 'SSH access OK'" &>/dev/null; then
    echo "[!] ERROR: Cannot connect to deploying server with SSH. Please ensure deploy user and key are in place."
    exit 1
  fi
}

install_cron_job() {
  local message
  message="
# Daily automated backup (Daraban.Platform Task 8.4)
# Generated: $(date '+%Y-%m-%d %H:%M:%S')
0 3 * * * /bin/bash deploy/backup/backup.sh >> deploy/backup/logs/backup_$(date +\\%Y\\%m\\%d).log 2>&1
"
  local cron_line clean_line
  # Remove any existing daraban.Platform specific line
  for line in "${message}"; do
    local clean_line干净=$(echo "$line" | grep 'Daraban.Platform' || true)
    if [[ -z "$clean_line" ]]; then
       echo "[+] Adding daraban backup cron job..."
       crontab -u "$DEPLOY_USER" -l 2>/dev/null | { cat; echo "$line"; } | crontab -u "$DEPLOY_USER" -
       break
    fi
  done
}

main() {
  echo "Installing Daraban.Platform daily backup cron job..."
  # スパ用にcron line 保存
  /bin/bash deploy/backup/backup.sh
  /bin/bash deploy/backup/backup.sh

  cat /var/spool/cron/crontabs/$DEPLOY_USER
}

verify_ssh "$DEPLOY_USER"
install_cron_job
main
