#!/usr/bin/env bash
# =============================================================================
# init-letsencrypt.sh — first-time TLS bootstrap for the production edge (Task 8.4).
#
# Run ONCE on the server before the first `docker compose up`:
#   DOMAIN=daraban.example.com EMAIL=ops@example.com ./deploy/nginx/init-letsencrypt.sh
#
# Why the dance with a dummy certificate: nginx refuses to start when the
# ssl_certificate paths do not exist, and certbot's webroot plugin needs a RUNNING
# nginx to answer the ACME challenge. So: (1) mint a throwaway self-signed cert
# so nginx boots, (2) start nginx, (3) replace the dummy with a real certificate,
# (4) reload. Renewals afterwards need none of this (`certbot renew` in cron).
#
# Requirements: DOMAIN resolvable to this host, ports 80+443 reachable from the
# internet, docker compose available. Fails closed (set -euo pipefail) on any step.
# (Backticks are banned in the ${VAR:?...} messages below: inside double quotes they
# would parse as command substitution and break the script.)
# =============================================================================
set -euo pipefail

# All paths below are relative to the production checkout. Running from anywhere
# else would scatter cert directories across the filesystem and then fail at the
# compose step -- refuse early with a usable message instead.
if [[ ! -f docker-compose.yml || ! -f docker-compose.prod.yml ]]; then
  echo "Run this from the production checkout root (/opt/daraban), not $(pwd)" >&2
  exit 1
fi

DOMAIN="${DOMAIN:?Set DOMAIN to the public hostname, e.g. DOMAIN=daraban.example.com}"
EMAIL="${EMAIL:?Set EMAIL for expiry notices, e.g. EMAIL=ops@example.com}"

RSA_KEY_SIZE=4096
CERT_ROOT="./deploy/nginx/certs"
WEBROOT="./deploy/nginx/certbot-webroot"
LIVE_DIR="$CERT_ROOT/live/$DOMAIN"

echo "==> [1/6] Preparing certificate directories"
mkdir -p "$WEBROOT" "$CERT_ROOT"

echo "==> [2/6] Minting a throwaway self-signed certificate so nginx can boot"
mkdir -p "$LIVE_DIR"
openssl req -x509 -nodes -newkey "rsa:$RSA_KEY_SIZE" -days 1 \
  -keyout "$LIVE_DIR/privkey.pem" \
  -out "$LIVE_DIR/fullchain.pem" \
  -subj "/CN=$DOMAIN" >/dev/null 2>&1
ln -sfn "$DOMAIN" "$CERT_ROOT/live/current"
echo "    dummy cert in place; nginx will start"

echo "==> [3/6] Starting nginx (production overlay)"
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --force-recreate nginx

echo "==> [4/6] Deleting the dummy certificate"
rm -rf "$LIVE_DIR"
# The 'current' symlink dangles for a moment -- that is fine, nginx already
# loaded the dummy into memory and the reload in step 6 re-reads the paths.

echo "==> [5/6] Requesting the real certificate (HTTP-01 via webroot)"
docker compose -f docker-compose.yml -f docker-compose.prod.yml run --rm --entrypoint "" certbot \
  certbot certonly --webroot -w /var/www/certbot \
    --email "$EMAIL" \
    --rsa-key-size "$RSA_KEY_SIZE" \
    --agree-tos --no-eff-email --force-renewal \
    -d "$DOMAIN"

echo "==> [6/6] Pointing 'current' at the real certificate and reloading nginx"
ln -sfn "$DOMAIN" "$CERT_ROOT/live/current"
docker compose -f docker-compose.yml -f docker-compose.prod.yml exec nginx nginx -s reload

echo "TLS bootstrap complete for $DOMAIN."
echo "Renewals: certbot renew runs from host cron -- see docs/10-Backup-Restore-Runbook.md."
