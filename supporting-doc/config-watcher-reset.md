# Reset and verify config watcher (manual)

This guide re-creates `outline-config-watcher` manually, without using `install.sh`.

Use it when:
- config changes are not applied automatically,
- watcher logs show errors,
- watcher service is missing or failed.

## Prerequisites

- You can run `sudo` on the host.
- Docker is running.
- You know your install directory (default: `/opt/outline-manager`).

Set your install path once:

```bash
INSTALL_DIR=/opt/outline-manager
```

## 1) Check current watcher state

```bash
sudo systemctl status outline-config-watcher --no-pager
sudo systemctl is-enabled outline-config-watcher 2>/dev/null || true
sudo systemctl is-active outline-config-watcher 2>/dev/null || true
ls -l /etc/systemd/system/outline-config-watcher.service 2>/dev/null || true
ls -l "$INSTALL_DIR/config-watcher.sh" 2>/dev/null || true
```

What each line does and what to expect:

- `sudo systemctl status outline-config-watcher --no-pager`
  - Shows full service details (loaded unit path, active state, recent logs).
  - Healthy watcher usually shows `Loaded: loaded` and `Active: active (running)`.
  - If missing, you may see `Unit outline-config-watcher.service could not be found`.

- `sudo systemctl is-enabled outline-config-watcher 2>/dev/null || true`
  - Checks boot behavior.
  - Expected values:
    - `enabled`: starts automatically on reboot.
    - `disabled`: exists but will not auto-start.
    - no output/error: often means unit is missing.

- `sudo systemctl is-active outline-config-watcher 2>/dev/null || true`
  - Checks runtime state only.
  - Expected values:
    - `active`: currently running.
    - `inactive`: stopped.
    - `failed`: crashed or exited with error.

- `ls -l /etc/systemd/system/outline-config-watcher.service 2>/dev/null || true`
  - Verifies the systemd unit file exists on disk.
  - If present, shows file metadata; if missing, no output.

- `ls -l "$INSTALL_DIR/config-watcher.sh" 2>/dev/null || true`
  - Verifies the watcher script exists in your install dir.
  - If present, confirm it has execute permission (`x` bits in mode, e.g. `-rwxr-xr-x`).

How to interpret quickly:

- Unit + script exist, and service is `active`/`enabled`: watcher is installed and running.
- Unit exists but `inactive`/`failed`: watcher exists but needs restart/fix.
- Unit/script missing: proceed with full remove + recreate flow below.

Optional logs:

```bash
sudo journalctl -u outline-config-watcher -n 100 --no-pager
tail -n 100 "$INSTALL_DIR/config-watcher.log" 2>/dev/null || true
```

## 2) Remove existing watcher

```bash
sudo systemctl stop outline-config-watcher.service 2>/dev/null || true
sudo systemctl disable outline-config-watcher.service 2>/dev/null || true
sudo rm -f /etc/systemd/system/outline-config-watcher.service
sudo rm -f "$INSTALL_DIR/config-watcher.sh" "$INSTALL_DIR/config-watcher.log"
sudo systemctl daemon-reload
```

## 3) Create a new watcher

1) Install dependency (if missing):

```bash
command -v inotifywait >/dev/null || (sudo apt-get update && sudo apt-get install -y inotify-tools)
```

2) Create watcher script:

```bash
sudo tee "$INSTALL_DIR/config-watcher.sh" > /dev/null <<'EOF'
#!/bin/bash

CONFIG_FILE="__INSTALL_DIR__/outline/config/config.yaml"
CONTAINER_NAME="outline-server"
LOG_FILE="__INSTALL_DIR__/config-watcher.log"
DOCKER_BIN=$(command -v docker 2>/dev/null || echo /usr/bin/docker)
INOTIFY_BIN=$(command -v inotifywait 2>/dev/null || echo /usr/bin/inotifywait)

exec >> "$LOG_FILE" 2>&1

echo "Config watcher started, monitoring: $CONFIG_FILE"

send_reload() {
    local pid
    if [[ ! -x "$DOCKER_BIN" ]]; then
        echo "$(date '+%Y-%m-%d %H:%M:%S') - docker binary not found: $DOCKER_BIN"
        return 1
    fi

    pid=$("$DOCKER_BIN" exec "$CONTAINER_NAME" sh -c 'pgrep -f "outline-ss-server" | grep -v "^1$" | head -n1' 2>/dev/null)

    if [[ -z "$pid" ]]; then
        pid=$("$DOCKER_BIN" exec "$CONTAINER_NAME" sh -c 'for p in /proc/[0-9]*; do name=$(cat "$p/comm" 2>/dev/null || true); if [ "$name" = "outline-ss-server" ] && [ "${p##*/}" != "1" ]; then echo "${p##*/}"; break; fi; done' 2>/dev/null)
    fi

    if [[ -z "$pid" ]]; then
        echo "$(date '+%Y-%m-%d %H:%M:%S') - Failed to find outline-ss-server PID"
        return 1
    fi

    if "$DOCKER_BIN" exec "$CONTAINER_NAME" sh -c "kill -HUP $pid" 2>/dev/null; then
        echo "$(date '+%Y-%m-%d %H:%M:%S') - Reload signal sent to outline-ss-server (PID $pid)"
        return 0
    fi

    echo "$(date '+%Y-%m-%d %H:%M:%S') - Failed to send reload signal to outline-ss-server"
    return 1
}

"$INOTIFY_BIN" -m -e modify,close_write,move,create,attrib "$CONFIG_FILE" |
while read -r directory events filename; do
    echo "$(date '+%Y-%m-%d %H:%M:%S') - Config file changed, reloading Outline server..."
    send_reload
done
EOF

sudo sed -i "s|__INSTALL_DIR__|$INSTALL_DIR|g" "$INSTALL_DIR/config-watcher.sh"
sudo chmod +x "$INSTALL_DIR/config-watcher.sh"
```

3) Create systemd unit:

```bash
sudo tee /etc/systemd/system/outline-config-watcher.service > /dev/null <<EOF
[Unit]
Description=Outline Config File Watcher
After=docker.service
Requires=docker.service

[Service]
Type=simple
ExecStart=$INSTALL_DIR/config-watcher.sh
Restart=always
RestartSec=10
StandardOutput=append:$INSTALL_DIR/config-watcher.log
StandardError=append:$INSTALL_DIR/config-watcher.log

[Install]
WantedBy=multi-user.target
EOF
```

4) Enable and start service:

```bash
sudo systemctl daemon-reload
sudo systemctl enable outline-config-watcher.service
sudo systemctl restart outline-config-watcher.service
```

## 4) Verify it works

1) Service is active:

```bash
sudo systemctl status outline-config-watcher --no-pager
sudo systemctl is-active outline-config-watcher
```

2) Container exists/running:

```bash
docker ps --format '{{.Names}}' | grep -x outline-server
```

3) Trigger config change and confirm reload log:

```bash
sudo touch "$INSTALL_DIR/outline/config/config.yaml"
sleep 2
tail -n 50 "$INSTALL_DIR/config-watcher.log"
```

Expected lines:
- `Config file changed, reloading Outline server...`
- `Reload signal sent to outline-ss-server (PID ...)`

## Troubleshooting

- If service is `failed`:
  ```bash
  sudo journalctl -u outline-config-watcher -e --no-pager
  ```
- If you see errors like `line 9: : No such file or directory` or `line 40: : command not found`:
  - the script was generated with expanded variables,
  - recreate `config-watcher.sh` using the exact Step 3 block with `<<'EOF'`,
  - then run `sudo systemctl restart outline-config-watcher`.
- If inotify is missing:
  ```bash
  sudo apt-get update && sudo apt-get install -y inotify-tools
  ```
- If reload fails with missing process/container:
  - check container name with `docker compose ps`,
  - verify Outline process is running inside container.
- If no change events are detected:
  - confirm `CONFIG_FILE` path in `config-watcher.sh` is correct for your install dir.
