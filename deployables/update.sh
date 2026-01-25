#!/bin/bash

# Resolve the directory this script lives in
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Change to the directory where your docker-compose.yml is located
cd "$SCRIPT_DIR"

# Pull the latest version of the Docker image
docker compose pull

# Recreate containers with the new image (if there's an update) and remove old containers
docker compose up -d --remove-orphans

# Optionally, remove unused images to free up space
docker image prune -f
