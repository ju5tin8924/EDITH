#!/usr/bin/env bash
# One-time OCI ARM64 VM bootstrap script for EDITH (Ubuntu / Oracle Linux aarch64).
# Usage on OCI VM (144.21.52.172):
#   EDITH_PG_PASSWORD='<strong-pg-pass>' OPENSEARCH_PASSWORD='<strong-os-pass>' sudo -E bash bootstrap-vm.sh
set -euo pipefail

if [[ "$(uname -m)" != "aarch64" && "$(uname -m)" != "arm64" ]]; then
  echo "ERROR: Expected aarch64 VM." >&2
  exit 1
fi

if [[ -z "${EDITH_PG_PASSWORD:-}" || -z "${OPENSEARCH_PASSWORD:-}" ]]; then
  echo "ERROR: Set EDITH_PG_PASSWORD and OPENSEARCH_PASSWORD before running bootstrap-vm.sh" >&2
  exit 1
fi

echo "==> Installing system packages (rsync, nginx, curl, docker)"
if command -v apt-get >/dev/null 2>&1; then
  apt-get update
  apt-get install -y rsync nginx curl ca-certificates docker.io docker-compose-v2 iptables-persistent
elif command -v dnf >/dev/null 2>&1; then
  dnf install -y rsync nginx curl ca-certificates docker
fi
systemctl enable --now docker nginx

echo "==> Installing .NET 10 ASP.NET Core Runtime (arm64)"
if ! /usr/share/dotnet/dotnet --list-runtimes 2>/dev/null | grep -q "Microsoft.AspNetCore.App 10\."; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- \
    --channel 10.0 --runtime aspnetcore --architecture arm64 --install-dir /usr/share/dotnet
  ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
fi

echo "==> Opening HTTP/HTTPS ports in host firewall"
if command -v firewall-cmd >/dev/null 2>&1 && systemctl is-active --quiet firewalld; then
  firewall-cmd --permanent --add-service=http --add-service=https
  firewall-cmd --reload
elif command -v iptables >/dev/null 2>&1; then
  iptables -C INPUT -p tcp --dport 80 -j ACCEPT 2>/dev/null || iptables -I INPUT 1 -p tcp --dport 80 -j ACCEPT
  iptables -C INPUT -p tcp --dport 443 -j ACCEPT 2>/dev/null || iptables -I INPUT 1 -p tcp --dport 443 -j ACCEPT
  netfilter-persistent save 2>/dev/null || true
fi

echo "==> Starting PostgreSQL 17 & OpenSearch 2.x (ARM64) containers bound to 127.0.0.1"
sysctl -w vm.max_map_count=262144 >/dev/null
echo "vm.max_map_count=262144" > /etc/sysctl.d/99-opensearch.conf

docker network inspect edith-net >/dev/null 2>&1 || docker network create edith-net

if ! docker ps -a --format '{{.Names}}' | grep -qx 'edith_postgres_17'; then
  docker run -d --name edith_postgres_17 --restart unless-stopped \
    --network edith-net -p 127.0.0.1:5432:5432 \
    -e POSTGRES_USER=edith_admin \
    -e POSTGRES_PASSWORD="${EDITH_PG_PASSWORD}" \
    -e POSTGRES_DB=edith_aml \
    -v edith_pgdata:/var/lib/postgresql/data \
    postgres:17
fi

if ! docker ps -a --format '{{.Names}}' | grep -qx 'edith_opensearch_2'; then
  docker run -d --name edith_opensearch_2 --restart unless-stopped \
    --network edith-net -p 127.0.0.1:9200:9200 \
    -e cluster.name=edith-screening-cluster \
    -e node.name=edith-os-node \
    -e discovery.type=single-node \
    -e "OPENSEARCH_JAVA_OPTS=-Xms2g -Xmx2g" \
    -e "OPENSEARCH_INITIAL_ADMIN_PASSWORD=${OPENSEARCH_PASSWORD}" \
    -v edith_osdata:/usr/share/opensearch/data \
    opensearchproject/opensearch:2.17.1
fi

echo "==> OCI ARM64 VM bootstrap complete."
