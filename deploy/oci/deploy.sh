#!/usr/bin/env bash
# Server-side deployment & health verification script for EDITH on OCI ARM64 (aarch64).
# Usage:
#   ./deploy.sh                  # Full production deployment on OCI VM
#   ./deploy.sh --self-check     # Syntax and secret-validation self-check
set -euo pipefail

APP_ROOT="${APP_ROOT:-/opt/edith}"
STAGING_DIR="${STAGING_DIR:-/tmp/edith-staging}"
WEB_ROOT="${WEB_ROOT:-/var/www/edith-portal}"
ENV_FILE="${ENV_FILE:-/etc/edith/edith.env}"
GATEWAY_URL="${GATEWAY_URL:-http://127.0.0.1:5000}"
SCREENING_URL="${SCREENING_URL:-http://127.0.0.1:5101}"

validate_env() {
  if [[ -z "${EDITH_PG_CONNECTION:-}" ]]; then
    echo "ERROR: EDITH_PG_CONNECTION is required in production." >&2
    return 1
  fi
  if [[ "${EDITH_PG_CONNECTION}" == *"EdithSecurePassword2026!"* ]]; then
    echo "ERROR: EDITH_PG_CONNECTION contains the insecure default development password." >&2
    return 1
  fi
  if [[ -z "${JWT_SECRET:-}" || ${#JWT_SECRET} -lt 32 ]]; then
    echo "ERROR: JWT_SECRET must be set and at least 32 characters long." >&2
    return 1
  fi
  if [[ "${JWT_SECRET}" == "EDITH_SUPER_SECURE_ENTERPRISE_AML_JWT_SECRET_KEY_2026_GCC_VALIDATION_STRING" ]]; then
    echo "ERROR: JWT_SECRET matches the prohibited development secret." >&2
    return 1
  fi
}

check_health() {
  local name="$1"
  local url="$2"
  local attempts="${3:-15}"
  local sleep_sec="${4:-2}"

  for ((i = 1; i <= attempts; i++)); do
    if response="$(curl -fsS --max-time 5 "${url}" 2>/dev/null)" && [[ "${response}" == *'"status":"Healthy"'* ]]; then
      echo "[OK] ${name} is healthy (${url})"
      return 0
    fi
    echo "[WAIT] ${name} not ready yet (attempt ${i}/${attempts})..."
    sleep "${sleep_sec}"
  done

  echo "ERROR: ${name} failed health check at ${url} after ${attempts} attempts." >&2
  return 1
}

if [[ "${1:-}" == "--self-check" ]]; then
  # Self-check: verify guard clauses reject missing/default secrets and accept valid ones.
  if (EDITH_PG_CONNECTION="" JWT_SECRET="" validate_env 2>/dev/null); then
    echo "Self-check failed: empty env should fail" >&2
    exit 1
  fi
  if (EDITH_PG_CONNECTION="Host=localhost;Password=EdithSecurePassword2026!" JWT_SECRET="0123456789abcdef0123456789abcdef" validate_env 2>/dev/null); then
    echo "Self-check failed: default PG password should fail" >&2
    exit 1
  fi
  EDITH_PG_CONNECTION="Host=10.0.0.251;Port=5432;Database=edith_aml;Username=edith_admin;Password=ProdStrongPassword!" \
  JWT_SECRET="0123456789abcdef0123456789abcdef0123456789abcdef" \
  validate_env
  echo "Self-check passed."
  exit 0
fi

if [[ -f "${STAGING_DIR}/.deploy.env" ]]; then
  # shellcheck disable=SC1091
  source "${STAGING_DIR}/.deploy.env"
  rm -f "${STAGING_DIR}/.deploy.env"
fi

ARCH="$(uname -m)"
if [[ "${ARCH}" != "aarch64" && "${ARCH}" != "arm64" ]]; then
  echo "ERROR: Expected ARM64 (aarch64) host, found ${ARCH}." >&2
  exit 1
fi

validate_env

echo "==> Writing production environment configuration to ${ENV_FILE}"
sudo install -d -m 0750 "$(dirname "${ENV_FILE}")"
sudo tee "${ENV_FILE}" >/dev/null <<EOF
ASPNETCORE_ENVIRONMENT=Production
DOTNET_ENVIRONMENT=Production
EDITH_PG_CONNECTION=${EDITH_PG_CONNECTION}
ConnectionStrings__EdithPostgres=${EDITH_PG_CONNECTION}
JWT_SECRET=${JWT_SECRET}
Jwt__Secret=${JWT_SECRET}
Jwt__Issuer=${JWT_ISSUER:-https://edith.aml.cloud}
Jwt__Audience=${JWT_AUDIENCE:-https://edith.aml.cloud}
OpenSearch__BaseUrl=${OPENSEARCH_URL:-https://127.0.0.1:9200}
OpenSearch__Username=${OPENSEARCH_USER:-admin}
OpenSearch__Password=${OPENSEARCH_PASSWORD:-}
OpenSearch__SkipSslValidation=${OPENSEARCH_SKIP_SSL_VALIDATION:-false}
EDITH_SMTP_USER=${EDITH_SMTP_USER:-notifications@edith.aml.cloud}
EDITH_SMTP_PASSWORD=${EDITH_SMTP_PASSWORD:-}
EOF
sudo chmod 0600 "${ENV_FILE}"

echo "==> Syncing staged artifacts from ${STAGING_DIR} to ${APP_ROOT} and ${WEB_ROOT}"
sudo install -d -m 0755 "${APP_ROOT}/gateway" "${APP_ROOT}/screening-api" "${APP_ROOT}/migrator" "${WEB_ROOT}"
sudo rsync -a --delete "${STAGING_DIR}/gateway/" "${APP_ROOT}/gateway/"
sudo rsync -a --delete "${STAGING_DIR}/screening-api/" "${APP_ROOT}/screening-api/"
sudo rsync -a --delete "${STAGING_DIR}/migrator/" "${APP_ROOT}/migrator/"
sudo rsync -a --delete "${STAGING_DIR}/portal/" "${WEB_ROOT}/"
sudo chmod +x "${APP_ROOT}/gateway/Edith.Gateway" "${APP_ROOT}/screening-api/Edith.Screening.Api" "${APP_ROOT}/migrator/Edith.Database.Migrator" 2>/dev/null || true

echo "==> Applying PostgreSQL 17 database migrations"
"${APP_ROOT}/migrator/Edith.Database.Migrator" --connection "${EDITH_PG_CONNECTION}"

echo "==> Installing systemd unit files and restarting services"
sudo cp "${STAGING_DIR}/deploy/edith-screening.service" /etc/systemd/system/edith-screening.service
sudo cp "${STAGING_DIR}/deploy/edith-gateway.service" /etc/systemd/system/edith-gateway.service
sudo systemctl daemon-reload
sudo systemctl enable edith-screening.service edith-gateway.service

# shortcut: rolling restart on single VM causes ~1-2s connection drop, upgrade to blue-green or OCI Load Balancer when running multiple nodes
sudo systemctl restart edith-screening.service
check_health "Edith.Screening.Api" "${SCREENING_URL}/health" 15 2 || {
  sudo journalctl -u edith-screening.service -n 50 --no-pager >&2
  exit 1
}

sudo systemctl restart edith-gateway.service
check_health "Edith.Gateway (/healthz)" "${GATEWAY_URL}/healthz" 15 2 || {
  sudo journalctl -u edith-gateway.service -n 50 --no-pager >&2
  exit 1
}
check_health "Edith.Gateway (/health)" "${GATEWAY_URL}/health" 5 2 || {
  sudo journalctl -u edith-gateway.service -n 50 --no-pager >&2
  exit 1
}

if systemctl is-active --quiet nginx; then
  echo "==> Updating Nginx reverse-proxy configuration and reloading"
  if [[ -d /etc/nginx/sites-available ]]; then
    sudo cp "${STAGING_DIR}/deploy/edith.nginx.conf" /etc/nginx/sites-available/edith.conf
    sudo ln -sf /etc/nginx/sites-available/edith.conf /etc/nginx/sites-enabled/edith.conf
    sudo rm -f /etc/nginx/sites-enabled/default
  else
    sudo cp "${STAGING_DIR}/deploy/edith.nginx.conf" /etc/nginx/conf.d/edith.conf
  fi
  sudo nginx -t
  sudo systemctl reload nginx
fi

echo "==> Deployment and post-deployment health checks succeeded on $(hostname -f 2>/dev/null || hostname)."
