# Outline SS Server Docker

This Docker setup runs the latest version of outline-ss-server with WebSocket support.

## Quick Start

### 1. Build and Run

```bash
docker-compose up -d
```

### 2. Configure

Edit `config.yaml` to customize:
- **secret**: Change the default secret key (use `openssl rand -hex 16`)
- **paths**: Modify WebSocket paths for TCP and UDP
- **port**: Adjust the listening port if needed

### 3. Generate Secure Configuration

```bash
# Generate a random secret
openssl rand -hex 16

# Generate random paths
echo "/$(openssl rand -hex 12)"  # TCP path
echo "/$(openssl rand -hex 12)"  # UDP path
```

### 4. Check Status

```bash
# View logs
docker-compose logs -f

# Check if running
docker-compose ps
```

## Configuration

The `config.yaml` file defines:
- **web.servers**: WebSocket server configuration
- **services.listeners**: TCP and UDP WebSocket endpoints
- **services.keys**: Shadowsocks encryption keys

## Client Configuration

Clients need:
- Server address: Your domain or IP
- Port: 9090 (or custom port)
- TCP Path: `/tcp-ws` (or custom)
- UDP Path: `/udp-ws` (or custom)
- Cipher: `chacha20-ietf-poly1305`
- Secret: Your configured secret

## Using with Nginx

For production, use Nginx as a reverse proxy:
1. Set container to listen on `127.0.0.1:9090`
2. Configure Nginx to proxy WebSocket connections
3. Add SSL/TLS certificates

## Notes

- The Dockerfile automatically downloads the latest release
- Configuration is mounted as read-only
- Container restarts automatically unless stopped
- Health checks monitor the server process
