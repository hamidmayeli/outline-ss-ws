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

# Parse command line arguments
show_usage() {
    echo "Usage: $0 -d <domain> -e <email> [-i <install_dir>] [--stable]"
    echo ""
    echo "Options:"
    echo "  -d    Domain name (required, e.g., example.com)"
    echo "  -e    Email for SSL certificate (required)"
    echo "  -i    Installation directory (optional, default: /opt/outline-manager)"
    echo "  --stable  Use the stable management app image tag"
    echo "  -h    Show this help message"
    echo ""
    echo "Example:"
    echo "  $0 -d example.com -e admin@example.com -i /opt/outline --stable"
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
        -h|--help)
            show_usage
            ;;
        *)
            print_error "Invalid option: $1"
            show_usage
            ;;
    esac
done

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
        lsb-release
    
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
    mkdir -p nginx/conf.d
    mkdir -p outline/config
    mkdir -p data
    mkdir -p prometheus
    
    print_success "Installation directory prepared"
}

# Download docker-compose configuration
download_docker_compose() {
    print_step "Downloading docker-compose configuration..."
    
    cd "$INSTALL_DIR"
    
    # Download docker-compose.yaml
    if ! wget -q "$REPO_URL/deployables/docker-compose.yaml" -O docker-compose.yaml; then
        print_error "Failed to download docker-compose.yaml"
        print_info "Creating default docker-compose.yaml..."
        create_default_docker_compose
    fi

    update_management_app_image
    
    print_success "Docker-compose configuration downloaded"
}

# Update management app image tag in docker-compose
update_management_app_image() {
    if [[ -f "$INSTALL_DIR/docker-compose.yaml" ]]; then
        sed -i "s|hamidmayeli/outline-manager:[^[:space:]]*|hamidmayeli/outline-manager:$MGMT_APP_TAG|" "$INSTALL_DIR/docker-compose.yaml"
    fi
}

# Create default docker-compose if download fails
create_default_docker_compose() {
    cat > docker-compose.yaml <<EOF
services:
  outline-server:
    image: hamidmayeli/outline-over-ws:latest
    container_name: outline-server
    restart: unless-stopped
    labels:
      - "autoheal=true"
    healthcheck:
      test: ["CMD-SHELL", "curl -fsS http://localhost:9091/metrics >/dev/null || exit 1"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 10s
    volumes:
      - ./outline/config/config.yaml:/etc/outline/config.yaml:ro
            - ./prometheus:/var/lib/prometheus

  management-app:
    image: hamidmayeli/outline-manager:$MGMT_APP_TAG
    container_name: management-app
    restart: unless-stopped
    labels:
      - "autoheal=true"
    healthcheck:
      test: ["CMD-SHELL", "curl -fsS http://localhost:80/ >/dev/null || exit 1"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 10s
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:80
      - Jwt__SecretKey=${JWT_SECRET}
      - AppSettings__Domain=\${DOMAIN}
      - AppSettings__TcpPath=\${TCP_PATH}
      - AppSettings__UdpPath=\${UDP_PATH}
      - AppSettings__OutlineConfigPath=/etc/outline/config.yaml
      - AppSettings__PrometheusUrl=http://outline-server:9092
      - DataDirectory=/app/data
    volumes:
      - ./data:/app/data
      - ./outline/config/config.yaml:/etc/outline/config.yaml

  nginx:
    image: nginx:alpine
    container_name: nginx-proxy
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
      - "443:443/udp"
    volumes:
      - ./nginx/conf.d:/etc/nginx/conf.d:ro
      - /etc/letsencrypt:/etc/letsencrypt:ro
    depends_on:
      outline-server:
        condition: service_healthy
      management-app:
        condition: service_healthy

  autoheal:
    image: willfarrell/autoheal:latest
    container_name: autoheal
    restart: unless-stopped
    environment:
      - AUTOHEAL_CONTAINER_LABEL=autoheal
      - AUTOHEAL_INTERVAL=10
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
EOF
}

# Generate configuration files
generate_configurations() {
    print_step "Generating configuration files..."
    
    # Generate random secrets
    OUTLINE_SECRET=$(openssl rand -hex 16)
    JWT_SECRET=$(openssl rand -hex 32)
    TCP_PATH="/$(openssl rand -hex 12)"
    UDP_PATH="/$(openssl rand -hex 12)"
    
    # Create outline config
    cat > "$INSTALL_DIR/outline/config/config.yaml" <<EOF
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
    
    # Create .env file for docker-compose
    cat > "$INSTALL_DIR/.env" <<EOF
DOMAIN=$DOMAIN
JWT_SECRET=$JWT_SECRET
TCP_PATH=$TCP_PATH
UDP_PATH=$UDP_PATH
EOF
    
    # Create Nginx configuration
    cat > "$INSTALL_DIR/nginx/conf.d/default.conf" <<EOF
# Redirect HTTP to HTTPS
server {
    listen 80;
    server_name $DOMAIN;
    return 301 https://\$host\$request_uri;
}

# HTTPS Server
server {
    listen 443 ssl http2;
    server_name $DOMAIN;

    # SSL Configuration
    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;

    # WebSocket paths to outline-server
    location $TCP_PATH {
        proxy_pass http://outline-server:9090;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_read_timeout 86400;
    }

    location $UDP_PATH {
        proxy_pass http://outline-server:9090;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_read_timeout 86400;
    }

    # Management API
    location /api/ {
        proxy_pass http://management-app/api/;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }

    # Frontend
    location / {
        proxy_pass http://management-app/;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }

    # Logging
    access_log /var/log/nginx/outline_access.log;
    error_log /var/log/nginx/outline_error.log;
}
EOF
    
    print_success "Configuration files generated"
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
    
    # Install inotify-tools if not present
    if ! command -v inotifywait &> /dev/null; then
        print_info "Installing inotify-tools..."
        sudo apt-get update -qq
        sudo apt-get install -y inotify-tools
    fi
    
    # Create watcher script
    cat > "$INSTALL_DIR/config-watcher.sh" <<'WATCHER_EOF'
#!/bin/bash

CONFIG_FILE="/opt/outline-manager/outline/config/config.yaml"
CONTAINER_NAME="outline-server"

echo "Config watcher started, monitoring: $CONFIG_FILE"

# Monitor for file modifications
inotifywait -m -e modify,close_write "$CONFIG_FILE" |
while read -r directory events filename; do
    echo "$(date '+%Y-%m-%d %H:%M:%S') - Config file changed, reloading Outline server..."
    
    # Send SIGHUP signal to outline-server container
    if docker kill --signal=SIGHUP "$CONTAINER_NAME" 2>/dev/null; then
        echo "$(date '+%Y-%m-%d %H:%M:%S') - Reload signal sent successfully"
    else
        echo "$(date '+%Y-%m-%d %H:%M:%S') - Failed to send reload signal"
    fi
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
        cat > "$INSTALL_DIR/renew-cert.sh" <<'EOF'
#!/bin/bash
certbot renew --quiet --post-hook "docker compose -f /opt/outline-manager/docker-compose.yaml restart nginx"
EOF
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
    echo "  Nginx: $INSTALL_DIR/nginx/conf.d/default.conf"
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
    verify_domain
    install_prerequisites
    configure_firewall
    obtain_ssl_certificate
    setup_install_directory
    download_docker_compose
    generate_configurations
    start_services
    setup_config_watcher
    setup_cron_jobs
    display_summary
}

# Run main function
main
