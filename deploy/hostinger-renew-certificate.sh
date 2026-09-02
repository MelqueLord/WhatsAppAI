#!/usr/bin/env bash
# Renew Let's Encrypt certificates and reload the containerized Nginx.
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_DIR"

domain="${1:-}"
[[ -n "$domain" ]] || { echo "Uso: $0 app.seudominio.com" >&2; exit 1; }

docker compose stop nginx
restart_nginx() {
  docker compose --profile production up -d nginx >/dev/null 2>&1 || true
}
trap restart_nginx EXIT

certbot certonly --standalone --non-interactive --agree-tos --keep-until-expiring \
  -d "$domain" --email "${LETSENCRYPT_EMAIL:?Defina LETSENCRYPT_EMAIL}" --no-eff-email

install -d -m 700 deploy/nginx/certs
install -m 644 "/etc/letsencrypt/live/$domain/fullchain.pem" deploy/nginx/certs/fullchain.pem
install -m 600 "/etc/letsencrypt/live/$domain/privkey.pem" deploy/nginx/certs/privkey.pem
docker compose --profile production up -d nginx
docker compose exec -T nginx nginx -t
trap - EXIT
echo "Certificado atualizado para $domain."
