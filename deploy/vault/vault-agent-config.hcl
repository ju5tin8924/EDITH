# HashiCorp Vault Agent Configuration for EDITH Microservices
# Enforces zero-static credentials with dynamic 1-hour lease rotation for PostgreSQL 17

pid_file = "/tmp/vault-agent-pid"

auto_auth {
  method "kubernetes" {
    mount_path = "auth/kubernetes"
    config = {
      role = "edith-app-role"
    }
  }

  sink "file" {
    config = {
      path = "/tmp/vault-token"
    }
  }
}

template {
  destination = "/vault/secrets/database.env"
  contents = <<EOF
{{- with secret "database/creds/edith-dynamic-role" -}}
POSTGRES_USER="{{ .Data.username }}"
POSTGRES_PASSWORD="{{ .Data.password }}"
{{- end -}}
EOF
}
