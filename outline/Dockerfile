# ============== Fetch/Prepare Stage (Runs Natively and Fast) ==============
FROM --platform=$BUILDPLATFORM debian:bookworm-slim AS fetcher

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    wget \
    tar \
    && rm -rf /var/lib/apt/lists/*

# Automatically injected by Docker Buildx
ARG TARGETARCH

WORKDIR /download

# Fetch the version once natively, map standard Docker TARGETARCH to Outline's release naming conventions
RUN LATEST_VERSION=$(curl -s https://api.github.com/repos/OutlineFoundation/tunnel-server/releases/latest | grep '"tag_name"' | sed -E 's/.*"v([^"]+)".*/\1/') && \
    if [ "$TARGETARCH" = "amd64" ]; then \
        ARCH_SUFFIX="x86_64"; \
    elif [ "$TARGETARCH" = "arm64" ]; then \
        ARCH_SUFFIX="arm64"; \
    else \
        ARCH_SUFFIX="$TARGETARCH"; \
    fi && \
    echo "Downloading Outline Server version: $LATEST_VERSION for architecture: $ARCH_SUFFIX" && \
    wget -q https://github.com/OutlineFoundation/tunnel-server/releases/download/v${LATEST_VERSION}/outline-ss-server_${LATEST_VERSION}_linux_${ARCH_SUFFIX}.tar.gz && \
    tar -xzf outline-ss-server_${LATEST_VERSION}_linux_${ARCH_SUFFIX}.tar.gz && \
    mv outline-ss-server /usr/local/bin/

# ============== Final Multi-Arch Target Stage ==============
FROM debian:bookworm-slim

# Set environment variables
ENV OUTLINE_VERSION=latest \
    OUTLINE_CONFIG=/etc/outline/config.yaml \
    PROMETHEUS_RETENTION=30d

# Install dependencies (This installs native arm64 or amd64 Prometheus depending on target platform)
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    procps \
    prometheus && \
    rm -rf /var/lib/apt/lists/*

# Copy the natively downloaded and extracted binary directly from the fetcher stage
COPY --from=fetcher /usr/local/bin/outline-ss-server /usr/local/bin/outline-ss-server
RUN chmod +x /usr/local/bin/outline-ss-server

# Create config directory
RUN mkdir -p /etc/outline

# Create Prometheus config to scrape outline metrics
RUN mkdir -p /etc/prometheus && \
    printf '%s\n' \
    'global:' \
    '  scrape_interval: 15s' \
    'scrape_configs:' \
    '  - job_name: outline-ss-server' \
    '    static_configs:' \
    '      - targets: ["localhost:9091"]' \
    > /etc/prometheus/prometheus.yml

# Expose ports
# 9090 for the web server (WebSocket connections)
# 9091 for outline-ss-server Prometheus metrics
# 9092 for Prometheus UI/API
EXPOSE 9090 9091 9092

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -fsS http://localhost:9091/metrics >/dev/null || exit 1

# Set working directory
WORKDIR /etc/outline

# Run outline-ss-server and Prometheus
CMD ["/bin/sh", "-c", "prometheus --config.file=/etc/prometheus/prometheus.yml --storage.tsdb.path=/var/lib/prometheus --storage.tsdb.retention.time=${PROMETHEUS_RETENTION} --web.listen-address=0.0.0.0:9092 & /usr/local/bin/outline-ss-server -config /etc/outline/config.yaml -metrics 0.0.0.0:9091"]
