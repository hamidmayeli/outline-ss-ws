#!/bin/bash

# Renew SSL Certificate Script
# Uses certbot webroot mode with the Docker nginx container serving ACME challenges.
# Designed to run as a daily cron job; certbot only renews when within 30 days of expiry.

set -e

# Resolve the directory this script lives in
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Read DOMAIN from .env
if [[ ! -f "$SCRIPT_DIR/.env" ]]; then
    echo "Error: $SCRIPT_DIR/.env not found"
    exit 1
fi

DOMAIN=$(grep -E "^DOMAIN=" "$SCRIPT_DIR/.env" | tail -n1 | cut -d= -f2-)

if [[ -z "$DOMAIN" ]]; then
    echo "Error: DOMAIN not set in $SCRIPT_DIR/.env"
    exit 1
fi

# Certbot needs write access to its default config/log directories.
# Re-run as root when needed so existing deployments can renew certs in place.
if [[ $EUID -ne 0 ]]; then
    if command -v sudo >/dev/null 2>&1; then
        echo "$(date '+%Y-%m-%d %H:%M:%S') - Re-running as root via sudo so Certbot can update the existing certificate store"
        exec sudo -n env DOMAIN="$DOMAIN" SCRIPT_DIR="$SCRIPT_DIR" bash "$0"
    fi

    echo "$(date '+%Y-%m-%d %H:%M:%S') - Error: Certbot needs root privileges to write to /etc/letsencrypt and /var/log/letsencrypt"
    exit 1
fi

mkdir -p /var/log/letsencrypt /etc/letsencrypt

# Ensure webroot directory exists for ACME challenges
mkdir -p "$SCRIPT_DIR/certbot-webroot/.well-known/acme-challenge"

echo "$(date '+%Y-%m-%d %H:%M:%S') - Starting certificate renewal check for $DOMAIN"

# Attempt renewal using webroot validation
# --cert-name: explicit target certificate name to prevent -0001 suffix directories
# --expand: safely adds new domains to the existing certificate
# --keep-until-expiring: only renew if cert is within 30 days of expiry
if certbot certonly \
    --webroot \
    -w "$SCRIPT_DIR/certbot-webroot" \
    --cert-name "$DOMAIN" \
    -d "$DOMAIN" \
    --non-interactive \
    --agree-tos \
    --keep-until-expiring; then

    echo "$(date '+%Y-%m-%d %H:%M:%S') - Certbot succeeded, verifying certificate paths"

    LIVE_DIR="/etc/letsencrypt/live/$DOMAIN"

    if [[ ! -f "$LIVE_DIR/fullchain.pem" || ! -f "$LIVE_DIR/privkey.pem" ]]; then
        echo "$(date '+%Y-%m-%d %H:%M:%S') - Error: expected certificate files are missing under $LIVE_DIR"
        exit 1
    fi

    docker restart nginx-proxy
    echo "$(date '+%Y-%m-%d %H:%M:%S') - Nginx restarted successfully"
else
    echo "$(date '+%Y-%m-%d %H:%M:%S') - Error: Certbot failed to renew certificate"
    exit 1
fi
