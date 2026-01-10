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

# Parse command line arguments
show_usage() {
    echo "Usage: $0 -d <domain> -e <email> [-i <install_dir>]"
    echo ""
    echo "Options:"
    echo "  -d    Domain name (required, e.g., example.com)"
    echo "  -e    Email for SSL certificate (required)"
    echo "  -i    Installation directory (optional, default: /opt/outline-manager)"
    echo "  -h    Show this help message"
    echo ""
    echo "Example:"
    echo "  $0 -d example.com -e admin@example.com -i /opt/outline"
    exit 1
}

while getopts "d:e:i:h" opt; do
    case $opt in
        d) DOMAIN="$OPTARG" ;;
        e) EMAIL="$OPTARG" ;;
        i) INSTALL_DIR="$OPTARG" ;;
        h) show_usage ;;
        \?) print_error "Invalid option: -$OPTARG"; show_usage ;;
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
    
    SERVER_IP=$(curl -s ifconfig.me || curl -s icanhazip.com)
    DOMAIN_IP=$(dig +short "$DOMAIN" | tail -n1)
    
    if [[ -z "$DOMAIN_IP" ]]; then
        print_error "Could not resolve domain: $DOMAIN"
        print_error "Please ensure your domain's DNS A record is configured correctly."
        exit 1
    fi
    
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
    
    sudo apt update
    sudo apt install -y \
        curl \
        wget \
        git \
        nginx \
        certbot \
        python3-certbot-nginx \
        docker.io \
        docker-compose \
        openssl \
        ufw
    
    # Start and enable Docker
    sudo systemctl start docker
    sudo systemctl enable docker
    
    # Add current user to docker group
    sudo usermod -aG docker $USER
    
    print_success "Prerequisites installed"
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
    
    print_success "Docker-compose configuration downloaded"
}

# Create default docker-compose if download fails
create_default_docker_compose() {
    cat > docker-compose.yaml <<EOF
services:
  outline-server:
    image: hamidmayeli/outline-over-ws:latest
    container_name: outline-server
    restart: unless-stopped
    volumes:
      - ./outline/config/config.yaml:/etc/outline/config.yaml:ro

  management-app:
    image: hamidmayeli/outline-manager:latest
    container_name: management-app
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - Jwt__SecretKey=${JWT_SECRET}
      - AppSettings__Domain=\${DOMAIN}
      - AppSettings__TcpPath=\${TCP_PATH}
      - AppSettings__UdpPath=\${UDP_PATH}
      - AppSettings__OutlineConfigPath=/etc/outline/config.yaml
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
    networks:
      - outline-network
    depends_on:
      - outline-server
      - management-app
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
        proxy_pass http://management-app:8080/api/;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }

    # Frontend
    location / {
        proxy_pass http://management-app:8080/;
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
    docker-compose pull
    docker-compose up -d
    
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
cd /opt/outline-manager
docker-compose pull
docker-compose up -d
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
certbot renew --quiet --post-hook "docker-compose -f /opt/outline-manager/docker-compose.yaml restart nginx"
EOF
        chmod +x "$INSTALL_DIR/renew-cert.sh"
    fi
    
    # Add cron jobs
    (crontab -l 2>/dev/null; echo "0 3 * * 0 $INSTALL_DIR/update.sh >> $INSTALL_DIR/update.log 2>&1") | crontab -
    (crontab -l 2>/dev/null; echo "0 2 * * * $INSTALL_DIR/renew-cert.sh >> $INSTALL_DIR/cert-renewal.log 2>&1") | crontab -
    
    print_success "Cron jobs configured"
    echo "  - Docker update: Weekly at 3:00 AM (Sunday)"
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
    echo "  View logs: docker-compose -f $INSTALL_DIR/docker-compose.yaml logs -f"
    echo "  Restart: docker-compose -f $INSTALL_DIR/docker-compose.yaml restart"
    echo "  Stop: docker-compose -f $INSTALL_DIR/docker-compose.yaml down"
    echo "  Start: docker-compose -f $INSTALL_DIR/docker-compose.yaml up -d"
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
