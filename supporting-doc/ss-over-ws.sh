#!/bin/bash

# Shadowsocks-over-WebSocket Automated Setup Script
# This script automates the complete setup process for Outline server with WebSocket support

set -e  # Exit on error

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored messages
print_success() { echo -e "${GREEN}[✓]${NC} $1"; }
print_error() { echo -e "${RED}[✗]${NC} $1"; }
print_info() { echo -e "${YELLOW}[i]${NC} $1"; }

# Get domain name from user
# Parse command line arguments
while getopts "d:e:r:" opt; do
  case $opt in
    d) DOMAIN="$OPTARG" ;;
    e) EMAIL="$OPTARG" ;;
    r) REPORT_URL="$OPTARG" ;;
    \?) print_error "Invalid option: -$OPTARG"; exit 1 ;;
  esac
done

# Prompt for missing values
if [[ -z "$DOMAIN" ]]; then
    read -p "Enter your domain name (e.g., example.com): " DOMAIN
fi

if [[ -z "$EMAIL" ]]; then
    read -p "Enter your email for SSL certificate: " EMAIL
fi

if [[ -z "$DOMAIN" ]] || [[ -z "$EMAIL" ]]; then

        print_error "Domain and email are required!"
        exit 1
    fi

    # Verify domain points to this server
    print_info "Verifying domain DNS configuration..."
    SERVER_IP=$(curl -s ifconfig.me)
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

    if [[ -z "$DOMAIN" ]] || [[ -z "$EMAIL" ]]; then
    print_error "Domain and email are required!"
    exit 1
fi

print_info "Starting Shadowsocks-over-WebSocket setup for domain: $DOMAIN"

# Step 1: Install prerequisites
print_info "Step 1: Installing prerequisites..."
sudo apt update
sudo apt install -y curl wget tar nginx certbot python3-certbot-nginx openssl vnstat
print_success "Prerequisites installed"

# Open required ports
print_info "Opening required ports..."
sudo iptables -A INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -A INPUT -p udp --dport 80 -j ACCEPT
sudo iptables -A INPUT -p tcp --dport 443 -j ACCEPT
sudo iptables -A INPUT -p udp --dport 443 -j ACCEPT
print_success "Ports opened"

# Step 2: Prepare Outline directory
print_info "Step 2: Preparing Outline directory..."
OUTDIR="/opt/outline"
sudo mkdir -p "${OUTDIR}"
sudo chown $USER:$USER "${OUTDIR}"
cd "${OUTDIR}"
print_success "Directory prepared: ${OUTDIR}"

# Step 3: Download Outline Server
print_info "Step 3: Downloading Outline Server..."
OUTLINE_RELEASE="1.9.2"
wget -q https://github.com/Jigsaw-Code/outline-ss-server/releases/download/v${OUTLINE_RELEASE}/outline-ss-server_${OUTLINE_RELEASE}_linux_x86_64.tar.gz
tar -xzf outline-ss-server_${OUTLINE_RELEASE}_linux_x86_64.tar.gz
sudo mv outline-ss-server /usr/local/bin/
sudo chmod +x /usr/local/bin/outline-ss-server
rm outline-ss-server_${OUTLINE_RELEASE}_linux_x86_64.tar.gz
print_success "Outline Server installed"
/usr/local/bin/outline-ss-server --version

# Step 4: Generate random secrets and paths
print_info "Step 4: Generating secrets and WebSocket paths..."
SECRET=$(openssl rand -hex 16)
TCP_PATH="/$(openssl rand -hex 12)"
UDP_PATH="/$(openssl rand -hex 12)"
print_success "Generated credentials:"
echo "  Secret: $SECRET"
echo "  TCP Path: $TCP_PATH"
echo "  UDP Path: $UDP_PATH"

# Step 5: Create outline-ws.yaml configuration
print_info "Step 5: Creating Outline configuration..."
sudo tee /etc/outline-ws.yaml > /dev/null <<EOF
web:
  servers:
    - id: ws-server
      listen:
        - "127.0.0.1:9090"

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
        secret: $SECRET
EOF
print_success "Configuration created at /etc/outline-ws.yaml"

# Step 6: Setup systemd service
print_info "Step 6: Setting up systemd service..."
sudo tee /etc/systemd/system/outline-ws.service > /dev/null <<EOF
[Unit]
Description=Outline WebSocket Server
After=network.target

[Service]
ExecStart=/usr/local/bin/outline-ss-server -config /etc/outline-ws.yaml
Restart=on-failure
User=root
LimitNOFILE=65535

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable outline-ws
sudo systemctl start outline-ws
sleep 2
sudo systemctl status --no-pager --full outline-ws
print_success "Outline service started"

# Step 7: Configure Nginx
print_info "Step 7: Configuring Nginx..."
sudo tee /etc/nginx/sites-available/outline.conf > /dev/null <<EOF
# Redirect HTTP to HTTPS
server {
    listen 80;
    server_name $DOMAIN;

    return 301 https://\$host\$request_uri;
}

# HTTPS Server Configuration
server {
    listen 443 ssl;
    server_name $DOMAIN;

    # SSL Certificate Settings (Will be managed by Certbot)
    ssl_certificate /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    root /var/www/html;
    index index.html index.htm;

    # Serve the static /ws file
    location = /ws {
        root /var/www/html;
        default_type "text/plain";
        try_files /ws =404;
    }

    # Context-specific WebSocket locations
    location $TCP_PATH {
        proxy_pass http://127.0.0.1:9090;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host \$host;
    }

    location $UDP_PATH {
        proxy_pass http://127.0.0.1:9090;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host \$host;
    }

    # Default response for root path
    location = / {
        return 200 "Shadowsocks-over-WS Ready\n";
    }

    # Optional catch-all location
    location / {
        proxy_pass http://127.0.0.1:9090;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
    }

    # Optional logging for debugging
    error_log /var/log/nginx/outline_error.log;
    access_log /var/log/nginx/outline_access.log;
}
EOF

# Enable Nginx site (create temporary config without SSL first)
sudo tee /etc/nginx/sites-available/outline-temp.conf > /dev/null <<EOF
server {
    listen 80;
    server_name $DOMAIN;

    location / {
        return 200 "Certbot verification";
    }
}
EOF

sudo ln -sf /etc/nginx/sites-available/outline-temp.conf /etc/nginx/sites-enabled/outline-temp.conf
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t
sudo systemctl restart nginx
print_success "Temporary Nginx configuration created"

# Step 8: Enable TLS via Certbot
print_info "Step 8: Obtaining SSL certificate..."
sudo certbot --nginx -d $DOMAIN -m $EMAIL --agree-tos --noninteractive --redirect
print_success "SSL certificate obtained"

# Apply final Nginx configuration
sudo rm -f /etc/nginx/sites-enabled/outline-temp.conf
sudo ln -sf /etc/nginx/sites-available/outline.conf /etc/nginx/sites-enabled/outline.conf
sudo nginx -t
sudo systemctl restart nginx
print_success "Final Nginx configuration applied"

# Step 9: Create dynamic access key
print_info "Step 9: Creating dynamic access key..."
sudo tee /var/www/html/ws > /dev/null <<EOF
transport:
  \$type: tcpudp

  tcp:
    \$type: shadowsocks
    endpoint:
      \$type: websocket
      url: wss://$DOMAIN$TCP_PATH
    cipher: chacha20-ietf-poly1305
    secret: $SECRET

  udp:
    \$type: shadowsocks
    endpoint:
      \$type: websocket
      url: wss://$DOMAIN$UDP_PATH
    cipher: chacha20-ietf-poly1305
    secret: $SECRET

$([[ -n "$REPORT_URL" ]] && cat <<REPORTER
reporter:
  \$type: http
  request:
    url: ${REPORT_URL}
  interval: 24h
  enable_cookies: true
REPORTER
)
EOF
sudo chmod 644 /var/www/html/ws
print_success "Access key created at /var/www/html/ws"

# Step 10: Configure firewall
print_info "Step 10: Configuring firewall..."
if command -v ufw &> /dev/null; then
    sudo ufw allow 80,443/tcp
    sudo ufw --force enable
    print_success "UFW firewall configured"
else
    print_info "UFW not found, skipping firewall configuration"
fi

# Final output
echo ""
print_success "═══════════════════════════════════════════════════════════"
print_success "Setup completed successfully!"
print_success "═══════════════════════════════════════════════════════════"
echo ""
print_info "Your Shadowsocks-over-WebSocket server is ready!"
echo ""
echo "Share this access link with clients:"
echo ""
echo -e "${GREEN}ssconf://$DOMAIN/ws${NC}"
echo ""
echo "Or manually share the YAML file located at:"
echo "/var/www/html/ws"
echo ""
print_info "Configuration details:"
echo "  Domain: $DOMAIN"
echo "  Secret: $SECRET"
echo "  TCP Path: $TCP_PATH"
echo "  UDP Path: $UDP_PATH"
echo ""
print_info "Service status:"
sudo systemctl status --no-pager outline-ws | head -n 5
echo ""
print_success "Setup complete! Test your connection with the Outline Client."
