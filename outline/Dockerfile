FROM debian:bookworm-slim

# Set environment variables
ENV OUTLINE_VERSION=latest \
    OUTLINE_CONFIG=/etc/outline/config.yaml

# Install dependencies
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    wget \
    tar && \
    rm -rf /var/lib/apt/lists/*

# Download and install outline-ss-server
RUN LATEST_VERSION=$(curl -s https://api.github.com/repos/OutlineFoundation/tunnel-server/releases/latest | grep '"tag_name"' | sed -E 's/.*"v([^"]+)".*/\1/') && \
    echo "Installing Outline Server version: $LATEST_VERSION"  && \
    wget -q https://github.com/OutlineFoundation/tunnel-server/releases/download/v${LATEST_VERSION}/outline-ss-server_${LATEST_VERSION}_linux_x86_64.tar.gz  && \
    tar -xzf outline-ss-server_${LATEST_VERSION}_linux_x86_64.tar.gz && \
    mv outline-ss-server /usr/local/bin/ && \
    chmod +x /usr/local/bin/outline-ss-server && \
    rm outline-ss-server_${LATEST_VERSION}_linux_x86_64.tar.gz

# Verify installation
RUN outline-ss-server --version

# Create config directory
RUN mkdir -p /etc/outline

# Expose ports
# 9090 for the web server (WebSocket connections)
EXPOSE 9090

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD pgrep -x outline-ss-server || exit 1

# Set working directory
WORKDIR /etc/outline

# Run outline-ss-server
CMD ["/usr/local/bin/outline-ss-server", "-config", "/etc/outline/config.yaml"]
