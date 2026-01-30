# outline-ss-ws

## What is this repo
This repository provides a complete Outline SS + WS (Shadow socks over websocket) management system:
- An Outline server container with Prometheus metrics.
- A management app for client and report management.
- An Nginx reverse proxy that routes WebSocket traffic to Outline and HTTP traffic to the management app.
- A one-command installation script that provisions SSL, firewall rules, cron jobs, and Docker Compose.

## How to install
Prerequisites:
- A Linux server with sudo access.
- A domain name pointing to the server’s public IP.

Run the installer:
```sh
sudo bash -c "$(wget -qO- https://raw.githubusercontent.com/hamidmayeli/outline-ss-ws/refs/heads/main/install.sh)" \
  _ -d sample.com -e admin@sample.com
```

The installer downloads [deployables/docker-compose.yaml](deployables/docker-compose.yaml) and provisions everything end-to-end.

## What features does it have
- Outline SS server container with Prometheus metrics.
- Management API with JWT auth, client CRUD, and Outline config synchronization.
  - Client configs
  - Setting limits per client
  - `ssconf` support for dynamic access
  - Different reports and monitoring 
- Automated SSL setup, firewall configuration, and scheduled updates.

## How to contribute
Please open a PR and follow [CONTRIBUTING.md](CONTRIBUTING.md).

## Licensing
Free to use for personal or production use, but redistribution or copying of the source, images, or derivatives is not permitted.
