#!/bin/bash

# Outline SS Management System Installation Script
# This script automates the complete setup process for the Outline server with management system

set -e  # Exit on error

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored messages
print_success() { echo -e "${GREEN}[✓]${NC} $1"; }
print_error() { echo -e "${RED}[✗]${NC} $1"; }
print_info() { echo -e "${YELLOW}[i]${NC} $1"; }
print_step() { echo -e "${BLUE}[→]${NC} $1"; }

# Default values
INSTALL_DIR="/opt/outline-manager"
REPO_URL="https://raw.githubusercontent.com/hamidmayeli/outline-ss-ws/main"
MGMT_APP_TAG="latest"
START_STEP=1

# Parse command line arguments
show_usage() {
    echo "Usage: $0 -d <domain> -e <email> [-i <install_dir>] [--stable] [--continue <step>]"
    echo ""
    echo "Options:"
    echo "  -d    Domain name (required, e.g., example.com)"
    echo "  -e    Email for SSL certificate (required)"
    echo "  -i    Installation directory (optional, default: /opt/outline-manager)"
    echo "  --stable  Use the stable management app image tag"
    echo "  --continue <step>  Start from step number (1-11)"
    echo "  -h    Show this help message"
    echo ""
    echo "Example:"
    echo "  $0 -d example.com -e admin@example.com -i /opt/outline --stable"
    echo "  $0 -d example.com -e admin@example.com --continue 5"
    exit 1
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -d)
            DOMAIN="$2"
            shift 2
            ;;
        -e)
            EMAIL="$2"
            shift 2
            ;;
        -i)
            INSTALL_DIR="$2"
            shift 2
            ;;
        --stable)
            MGMT_APP_TAG="stable"
            shift
            ;;
        --continue)
            START_STEP="$2"
            shift 2
            ;;
        -h|--help)
            show_usage
            ;;
        *)
            print_error "Invalid option: $1"
            show_usage
            ;;
    esac
done

if ! [["$START_STEP" =~ ^[0-9]+$ ]] || [[ "$START_STEP" -lt 1 || "$START_STEP" -gt 11 ]]; then
    print_error "Invalid --continue value: $START_STEP (expected 1-11)"
    exit 1
fi

# Validate required parameters
if [[ -z "$DOMAIN" ]] || [[ -z "$EMAIL" ]]; then
    print_error "Domain and email are required!"
    show_usage
fi

# ============================================================================
# FUNCTION DEFINITIONS
# ============================================================================

# Verify domain DNS configuration
verify_domain() {
    print_step "Verifying domain DNS configuration..."
    
    # Try multiple IP detection services
    print_info "Detecting server IP address..."
    SERVER_IP=""
    
    # List of IP detection services to try
    IP_SERVICES=(
        "https://api.ipify.org"
        "https://icanhazip.com"
        "https://ifconfig.me/ip"
        "https://checkip.amazonaws.com"
        "https://ipecho.net/plain"
        "https://myexternalip.com/raw"
    )
    
    for service in "${IP_SERVICES[@]}"; do
        IP=$(curl -4 -s --max-time 5 "$service" 2>/dev/null | tr -d '[:space:]')
        
        # Validate that response is an IPv4 address
        if [[ $IP =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}$ ]]; then
            SERVER_IP="$IP"
            print_info "Server IP detected: $SERVER_IP (from $service)"
            break
        fi
    done
    
    if [[ -z "$SERVER_IP" ]]; then
        print_error "Failed to detect server IP address!"
        print_error "All IP detection services failed or returned invalid responses."
        print_info "Please manually verify your server's public IP and domain DNS configuration."
        read -p "Enter your server's public IP address manually: " MANUAL_IP
        
        if [[ $MANUAL_IP =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}$ ]]; then
            SERVER_IP="$MANUAL_IP"
        else
            print_error "Invalid IP address format!"
            exit 1
        fi
    fi
    
    # Resolve domain
    print_info "Resolving domain: $DOMAIN"
    DOMAIN_IP=$(dig +short "$DOMAIN" | grep -E '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$' | tail -n1)
    
    if [[ -z "$DOMAIN_IP" ]]; then
        print_error "Could not resolve domain: $DOMAIN"
        print_error "Please ensure your domain's DNS A record is configured correctly."
        exit 1
    fi
    
    print_info "Domain resolves to: $DOMAIN_IP"
    
    if [[ "$SERVER_IP" != "$DOMAIN_IP" ]]; then
        print_error "Domain DNS mismatch!"
        echo "  Server IP: $SERVER_IP"
        echo "  Domain IP: $DOMAIN_IP"
        echo ""
        print_error "Please update your domain's DNS A record to point to: $SERVER_IP"
        print_error "Wait for DNS propagation (usually 5-60 minutes) and try again."
        exit 1
    fi
    
    print_success "Domain verification passed (DNS points to $SERVER_IP)"
}

# Install system prerequisites
install_prerequisites() {
    print_step "Installing system prerequisites..."
    
    # Update package list
    sudo apt update
    
    # Install basic dependencies
    sudo apt install -y \
        curl \
        wget \
        git \
        nginx \
        certbot \
        python3-certbot-nginx \
        openssl \
        ufw \
        ca-certificates \
        gnupg \
        lsb-release \
        dnsutils
    
    # Install Docker from official repository
    print_info "Installing Docker from official repository..."
    
    # Remove old Docker versions if any
    sudo apt-get remove -y docker docker-engine docker.io containerd runc 2>/dev/null || true
    
    # Add Docker's official GPG key
    sudo install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --batch --yes --no-tty --dearmor -o /etc/apt/keyrings/docker.gpg
    sudo chmod a+r /etc/apt/keyrings/docker.gpg
    
    # Add Docker repository
    echo \
      "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
      $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
    
    # Update and install Docker Engine with Compose plugin
    sudo apt update
    sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
    
    # Start and enable Docker
    sudo systemctl start docker
    sudo systemctl enable docker
    
    # Add current user to docker group
    sudo usermod -aG docker $USER
    
    # Verify Docker Compose v2 installation
    docker compose version
    
    print_success "Prerequisites installed (Docker with Compose v2)"
}

# Configure firewall
configure_firewall() {
    print_step "Configuring firewall..."
    
    sudo ufw --force enable
    sudo ufw default deny incoming
    sudo ufw default allow outgoing
    sudo ufw allow ssh
    sudo ufw allow 80/tcp
    sudo ufw allow 443/tcp
    sudo ufw allow 443/udp
    
    print_success "Firewall configured"
}

# Obtain SSL certificate
obtain_ssl_certificate() {
    print_step "Obtaining SSL certificate..."
    
    # Create temporary Nginx configuration for Certbot
    sudo tee /etc/nginx/sites-available/temp-certbot.conf > /dev/null <<EOF
server {
    listen 80;
    server_name $DOMAIN;
    
    location / {
        return 200 "Certbot verification in progress...";
        add_header Content-Type text/plain;
    }
}
EOF
    
    sudo ln -sf /etc/nginx/sites-available/temp-certbot.conf /etc/nginx/sites-enabled/temp-certbot.conf
    sudo rm -f /etc/nginx/sites-enabled/default
    sudo nginx -t && sudo systemctl restart nginx
    
    # Obtain certificate
    sudo certbot certonly --nginx \
        -d "$DOMAIN" \
        -m "$EMAIL" \
        --agree-tos \
        --noninteractive \
        --redirect
    
    print_success "SSL certificate obtained"
}

# Setup installation directory
setup_install_directory() {
    print_step "Setting up installation directory: $INSTALL_DIR"
    
    sudo mkdir -p "$INSTALL_DIR"
    sudo chown $USER:$USER "$INSTALL_DIR"
    cd "$INSTALL_DIR"
    
    # Create directory structure
    mkdir -p nginx/templates
    mkdir -p outline/config
    mkdir -p data
    mkdir -p prometheus
    mkdir -p certbot-webroot
    
    print_success "Installation directory prepared"
}

# Download deployable configs (docker-compose and nginx template)
download_deployables() {
    print_step "Downloading deployable configuration files..."
    
    cd "$INSTALL_DIR"
    
    # Download docker-compose.yaml
    if ! wget -q "$REPO_URL/deployables/docker-compose.yaml" -O docker-compose.yaml; then
        print_error "Failed to download docker-compose.yaml"
        exit 1
    fi

    if ! wget -q "$REPO_URL/deployables/nginx/templates/default.conf.template" -O nginx/templates/default.conf.template; then
        print_error "Failed to download nginx.conf template"
        exit 1
    fi

    update_management_app_image
    
    print_success "Deployable configuration files downloaded"
}

# Update management app image tag in docker-compose
update_management_app_image() {
    if [[ -f "$INSTALL_DIR/docker-compose.yaml" ]]; then
        sed -i "s|hamidmayeli/outline-manager:[^[:space:]]*|hamidmayeli/outline-manager:$MGMT_APP_TAG|" "$INSTALL_DIR/docker-compose.yaml"
    fi
}


read_env_value() {
    local key="$1"
    if [[ -f "$INSTALL_DIR/.env" ]]; then
        grep -E "^${key}=" "$INSTALL_DIR/.env" | tail -n1 | cut -d= -f2- || true
    fi
}

read_outline_secret() {
    if [[ -f "$INSTALL_DIR/outline/config/config.yaml" ]]; then
        grep -E "^\s*secret:" "$INSTALL_DIR/outline/config/config.yaml" | tail -n1 | awk '{print $2}' || true
    fi
}

read_outline_paths() {
    if [[ -f "$INSTALL_DIR/outline/config/config.yaml" ]]; then
        grep -E "^\s*path:" "$INSTALL_DIR/outline/config/config.yaml" | awk '{print $2}' | tr -d '"' || true
    fi
}

# Generate runtime configuration files
generate_runtime_config() {
    print_step "Generating runtime configuration files..."

    local config_path="$INSTALL_DIR/outline/config/config.yaml"
    local env_path="$INSTALL_DIR/.env"
    local write_config=0

    JWT_SECRET=$(read_env_value "JWT_SECRET")
    TCP_PATH=$(read_env_value "TCP_PATH")
    UDP_PATH=$(read_env_value "UDP_PATH")
    OUTLINE_SECRET=$(read_outline_secret)

    if [[ -z "$TCP_PATH" || -z "$UDP_PATH" ]]; then
        mapfile -t existing_paths < <(read_outline_paths)
        TCP_PATH=${TCP_PATH:-${existing_paths[0]}}
        UDP_PATH=${UDP_PATH:-${existing_paths[1]}}
    fi

    OUTLINE_SECRET=${OUTLINE_SECRET:-$(openssl rand -hex 16)}
    JWT_SECRET=${JWT_SECRET:-$(openssl rand -hex 32)}
    TCP_PATH=${TCP_PATH:-"/$(openssl rand -hex 12)"}
    UDP_PATH=${UDP_PATH:-"/$(openssl rand -hex 12)"}

    if [[ ! -f "$config_path" ]]; then
        write_config=1
    fi

    if [[ "$write_config" -eq 1 ]]; then
        cat > "$config_path" <<EOF
web:
  servers:
    - id: ws-server
      listen:
        - "0.0.0.0:9090"

services:
  - listeners:
      - type: websocket-stream
        web_server: ws-server
        path: "$TCP_PATH"
      - type: websocket-packet
        web_server: ws-server
        path: "$UDP_PATH"
    keys:
      - id: 1
        cipher: chacha20-ietf-poly1305
        secret: $OUTLINE_SECRET
EOF
        else
                print_info "Existing outline config found; keeping current values"
        fi
    
    # Create .env file for docker-compose
    cat > "$env_path" <<EOF
DOMAIN=$DOMAIN
JWT_SECRET=$JWT_SECRET
TCP_PATH=$TCP_PATH
UDP_PATH=$UDP_PATH
EOF
    
    
    print_success "Runtime configuration files generated"
    print_info "Secrets saved:"
    echo "  Outline Secret: $OUTLINE_SECRET"
    echo "  TCP Path: $TCP_PATH"
    echo "  UDP Path: $UDP_PATH"
    echo "  JWT Secret: $JWT_SECRET"
}

# Start services
start_services() {
    print_step "Starting services with Docker Compose..."
    
    cd "$INSTALL_DIR"
    
    # Remove temporary Nginx config
    sudo rm -f /etc/nginx/sites-enabled/temp-certbot.conf
    sudo systemctl stop nginx
    
    # Start Docker Compose
    docker compose pull
    docker compose up -d
    
    print_success "Services started"
}

# Setup config file watcher
setup_config_watcher() {
    print_step "Setting up config file watcher..."

    # Clean existing watcher artifacts
    if systemctl list-unit-files | grep -q "^outline-config-watcher.service"; then
        sudo systemctl stop outline-config-watcher.service 2>/dev/null || true
        sudo systemctl disable outline-config-watcher.service 2>/dev/null || true
    fi
    sudo rm -f /etc/systemd/system/outline-config-watcher.service
    sudo rm -f "$INSTALL_DIR/config-watcher.sh" "$INSTALL_DIR/config-watcher.log"
    sudo systemctl daemon-reload
    
    # Install inotify-tools if not present
    if ! command -v inotifywait &> /dev/null; then
        print_info "Installing inotify-tools..."
        sudo apt-get update -qq
        sudo apt-get install -y inotify-tools
    fi
    
    # Create watcher script
    cat > "$INSTALL_DIR/config-watcher.sh" <<WATCHER_EOF
#!/bin/bash

CONFIG_FILE="$INSTALL_DIR/outline/config/config.yaml"
CONTAINER_NAME="outline-server"
LOG_FILE="$INSTALL_DIR/config-watcher.log"
DOCKER_BIN=\$(command -v docker 2>/dev/null || echo /usr/bin/docker)
INOTIFY_BIN=\$(command -v inotifywait 2>/dev/null || echo /usr/bin/inotifywait)

exec >> "\$LOG_FILE" 2>&1

echo "Config watcher started, monitoring: \$CONFIG_FILE"

send_reload() {
    local pid
    if [[ ! -x "\$DOCKER_BIN" ]]; then
        echo "\$(date '+%Y-%m-%d %H:%M:%S') - docker binary not found: \$DOCKER_BIN"
        return 1
    fi

    pid=\$("\$DOCKER_BIN" exec "\$CONTAINER_NAME" sh -c 'pgrep -f "outline-ss-server" | grep -v "^1$" | head -n1')

    if [[ -z "\$pid" ]]; then
        pid=\$("\$DOCKER_BIN" exec "\$CONTAINER_NAME" sh -c 'for p in /proc/[0-9]*; do name=\$(cat "$p/comm" 2>/dev/null || true); if [ "$name" = "outline-ss-server" ] && [ "${p##*/}" != "1" ]; then echo "${p##*/}"; break; fi; done')
    fi

    if [[ -z "\$pid" ]]; then
        echo "\$(date '+%Y-%m-%d %H:%M:%S') - Failed to find outline-ss-server PID"
        return 1
    fi

    if "\$DOCKER_BIN" exec "\$CONTAINER_NAME" sh -c "kill -HUP \$pid" 2>/dev/null; then
        echo "\$(date '+%Y-%m-%d %H:%M:%S') - Reload signal sent to outline-ss-server (PID \$pid)"
        return 0
    fi

    echo "\$(date '+%Y-%m-%d %H:%M:%S') - Failed to send reload signal to outline-ss-server"
    return 1
}

# Monitor for file modifications
"\$INOTIFY_BIN" -m -e modify,close_write,move,create,attrib "\$CONFIG_FILE" |
while read -r directory events filename; do
    echo "\$(date '+%Y-%m-%d %H:%M:%S') - Config file changed, reloading Outline server..."
    send_reload
done
WATCHER_EOF
    
    chmod +x "$INSTALL_DIR/config-watcher.sh"
    
    # Create systemd service
    sudo tee /etc/systemd/system/outline-config-watcher.service > /dev/null <<SERVICE_EOF
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
SERVICE_EOF
    
    # Enable and start the service
    sudo systemctl daemon-reload
    sudo systemctl enable outline-config-watcher.service
    sudo systemctl start outline-config-watcher.service
    
    print_success "Config watcher service configured and started"
}

# Setup cron jobs
setup_cron_jobs() {
    print_step "Setting up cron jobs..."
    
    # Download update script
    if wget -q "$REPO_URL/deployables/update.sh" -O "$INSTALL_DIR/update.sh"; then
        chmod +x "$INSTALL_DIR/update.sh"
    else
        print_info "Creating default update script..."
        cat > "$INSTALL_DIR/update.sh" <<'EOF'
#!/bin/bash
cd $INSTALL_DIR
docker compose pull
docker compose up -d
docker image prune -f
EOF
        chmod +x "$INSTALL_DIR/update.sh"
    fi
    
    # Download cert renewal script
    if wget -q "$REPO_URL/deployables/renew-cert.sh" -O "$INSTALL_DIR/renew-cert.sh"; then
        chmod +x "$INSTALL_DIR/renew-cert.sh"
    else
        print_info "Creating default cert renewal script..."
        cat > "$INSTALL_DIR/renew-cert.sh" <<'RENEW_EOF'
#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOMAIN=$(grep -E "^DOMAIN=" "$SCRIPT_DIR/.env" | tail -n1 | cut -d= -f2-)
mkdir -p "$SCRIPT_DIR/certbot-webroot/.well-known/acme-challenge"
if certbot certonly --webroot -w "$SCRIPT_DIR/certbot-webroot" -d "$DOMAIN" --non-interactive --agree-tos --keep-until-expiring; then
    docker restart nginx-proxy
fi
RENEW_EOF
        chmod +x "$INSTALL_DIR/renew-cert.sh"
    fi
    
    # Add cron jobs (ensure both are present, avoid duplicates)
    CRON_UPDATE_LINE="0 3 * * * $INSTALL_DIR/update.sh >> $INSTALL_DIR/update.log 2>&1"
    CRON_RENEW_LINE="0 2 * * * $INSTALL_DIR/renew-cert.sh >> $INSTALL_DIR/cert-renewal.log 2>&1"

    crontab -l 2>/dev/null | \
        grep -v -F "$INSTALL_DIR/update.sh" | \
        grep -v -F "$INSTALL_DIR/renew-cert.sh" | \
        { cat; echo "$CRON_UPDATE_LINE"; echo "$CRON_RENEW_LINE"; } | crontab -
    
    print_success "Cron jobs configured"
    echo "  - Docker update: Daily at 3:00 AM"
    echo "  - SSL renewal: Daily at 2:00 AM"
}

# Configure container memory limits based on server RAM
configure_memory_limits() {
    print_step "Configuring container memory limits based on server RAM..."

    local total_mem_kb
    total_mem_kb=$(grep MemTotal /proc/meminfo | awk '{print $2}')
    local total_mem_mb=$((total_mem_kb / 1024))

    print_info "Detected server RAM: ${total_mem_mb} MiB"

    local outline_limit management_limit nginx_limit

    if (( total_mem_mb <= 600 )); then
        # 512 MiB server
        outline_limit="200m"
        management_limit="128m"
        nginx_limit="64m"
    elif (( total_mem_mb <= 1200 )); then
        # 1 GiB server
        outline_limit="400m"
        management_limit="256m"
        nginx_limit="96m"
    elif (( total_mem_mb <= 2500 )); then
        # 2 GiB server
        outline_limit="900m"
        management_limit="512m"
        nginx_limit="128m"
    elif (( total_mem_mb <= 5000 )); then
        # 4 GiB server
        outline_limit="2g"
        management_limit="1g"
        nginx_limit="256m"
    else
        # 8+ GiB server
        outline_limit="4g"
        management_limit="2g"
        nginx_limit="512m"
    fi

    print_info "Setting limits: outline=${outline_limit}, management=${management_limit}, nginx=${nginx_limit}"

    local env_path="$INSTALL_DIR/.env"

    # Remove existing memory limit lines
    sed -i '/^OUTLINE_MEM_LIMIT=/d' "$env_path" 2>/dev/null || true
    sed -i '/^MANAGEMENT_MEM_LIMIT=/d' "$env_path" 2>/dev/null || true
    sed -i '/^NGINX_MEM_LIMIT=/d' "$env_path" 2>/dev/null || true

    # Append memory limits
    cat >> "$env_path" <<EOF
OUTLINE_MEM_LIMIT=$outline_limit
MANAGEMENT_MEM_LIMIT=$management_limit
NGINX_MEM_LIMIT=$nginx_limit
EOF

    # Restart containers to apply new limits
    cd "$INSTALL_DIR"
    docker compose up -d

    print_success "Memory limits configured and applied"
}

# Display final information
display_summary() {
    echo ""
    print_success "═══════════════════════════════════════════════════════════"
    print_success "Installation completed successfully!"
    print_success "═══════════════════════════════════════════════════════════"
    echo ""
    print_info "Your Outline Management System is ready!"
    echo ""
    echo "Access URLs:"
    echo "  Management UI: https://$DOMAIN"
    echo "  API: https://$DOMAIN/api"
    echo ""
    echo "Installation directory: $INSTALL_DIR"
    echo ""
    print_info "Useful commands:"
    echo "  View logs: docker compose -f $INSTALL_DIR/docker-compose.yaml logs -f"
    echo "  Restart: docker compose -f $INSTALL_DIR/docker-compose.yaml restart"
    echo "  Stop: docker compose -f $INSTALL_DIR/docker-compose.yaml down"
    echo "  Start: docker compose -f $INSTALL_DIR/docker-compose.yaml up -d"
    echo ""
    print_info "Configuration files:"
    echo "  Outline: $INSTALL_DIR/outline/config/config.yaml"
    echo "  Nginx: $INSTALL_DIR/nginx/templates/default.conf.template"
    echo "  Environment: $INSTALL_DIR/.env"
    echo ""
    print_success "Installation complete!"
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

main() {
    echo ""
    print_info "╔══════════════════════════════════════════════════════════╗"
    print_info "║  Outline SS Management System - Installation Script      ║"
    print_info "╚══════════════════════════════════════════════════════════╝"
    echo ""
    print_info "Domain: $DOMAIN"
    print_info "Email: $EMAIL"
    print_info "Installation Directory: $INSTALL_DIR"
    echo ""
    
    # Execute installation steps
    (( START_STEP <= 1 )) && install_prerequisites #1
    (( START_STEP <= 2 )) && verify_domain #2
    (( START_STEP <= 3 )) && configure_firewall #3
    (( START_STEP <= 4 )) && obtain_ssl_certificate #4
    (( START_STEP <= 5 )) && setup_install_directory #5
    (( START_STEP <= 6 )) && download_deployables #6
    (( START_STEP <= 7 )) && generate_runtime_config #7
    (( START_STEP <= 8 )) && start_services #8
    (( START_STEP <= 9 )) && setup_config_watcher #9
    (( START_STEP <= 10 )) && setup_cron_jobs #10
    (( START_STEP <= 11 )) && configure_memory_limits #11
    display_summary
}

# Run main function
main
