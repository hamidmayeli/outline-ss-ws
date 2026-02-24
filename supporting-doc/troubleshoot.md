# Troubleshooting (quick recovery first)

Use this guide for common service/connectivity issues when you do **not** need deep investigation first.

## Quick recovery (no investigation needed)

Use this when you want a fast recovery attempt in the directory that contains your active compose file.

```bash
cd [THE_INSTALLATION_PATH] # The installation path defaulted to `/opt/outline-manager/`
docker compose down
docker compose up -d
```

Optional check:

```bash
docker compose ps
```

If services come back healthy, no further action is needed.

---

## Scenario: clients were added but cannot connect

When newly added clients cannot connect, do both actions below:

1. Reset and verify the config watcher using:
   - [config-watcher-reset.md](config-watcher-reset.md)
2. Then restart containers again:

```bash
cd [THE_INSTALLATION_PATH] # The installation path defaulted to `/opt/outline-manager/`
docker compose down
docker compose up -d
```

Optional validation after restart:

```bash
docker compose ps
```

If still failing after this flow, proceed with deeper investigation (logs, service status, and config validation).
