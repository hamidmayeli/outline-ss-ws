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

# Ensure webroot directory exists for ACME challenges
mkdir -p "$SCRIPT_DIR/certbot-webroot/.well-known/acme-challenge"

echo "$(date '+%Y-%m-%d %H:%M:%S') - Starting certificate renewal check for $DOMAIN"

# Attempt renewal using webroot validation
# --keep-until-expiring: only renew if cert is within 30 days of expiry
# --webroot: use the directory served by Docker nginx at /.well-known/acme-challenge/
if certbot certonly \
    --webroot \
    -w "$SCRIPT_DIR/certbot-webroot" \
    -d "$DOMAIN" \
    --non-interactive \
    --agree-tos \
    --keep-until-expiring; then

    echo "$(date '+%Y-%m-%d %H:%M:%S') - Certbot succeeded, restarting nginx container"
    docker restart nginx-proxy
    echo "$(date '+%Y-%m-%d %H:%M:%S') - Nginx restarted successfully"
else
    echo "$(date '+%Y-%m-%d %H:%M:%S') - Error: Certbot failed to renew certificate"
    exit 1
fi
