#!/usr/bin/env bash
# First deployment and subsequent updates on a Hostinger Ubuntu VPS.
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_DIR"

required_files=(.env deploy/secrets/dataprotection.pfx deploy/nginx/certs/fullchain.pem deploy/nginx/certs/privkey.pem)
for file in "${required_files[@]}"; do
  [[ -f "$file" ]] || { echo "Arquivo obrigatório ausente: $PROJECT_DIR/$file" >&2; exit 1; }
done

chmod 600 .env deploy/secrets/dataprotection.pfx deploy/nginx/certs/privkey.pem
docker compose --profile production config >/dev/null
docker compose build
docker compose up -d postgres

for attempt in {1..30}; do
  if docker compose exec -T postgres pg_isready >/dev/null 2>&1; then break; fi
  [[ "$attempt" -eq 30 ]] && { echo "PostgreSQL não ficou saudável." >&2; exit 1; }
  sleep 2
done

docker compose run --rm migrate
docker compose --profile production up -d
docker compose ps

domain="$(sed -n 's/^DOMAIN=//p' .env | tail -n 1)"
if [[ -n "$domain" ]]; then
  curl --fail --silent --show-error --retry 10 --retry-delay 2 "https://${domain}/health/live" >/dev/null
  curl --fail --silent --show-error --retry 10 --retry-delay 2 "https://${domain}/health/ready" >/dev/null
  echo "Deploy concluído e health checks aprovados em https://${domain}."
else
  echo "Deploy concluído. DOMAIN não foi encontrado para executar o smoke test HTTPS."
fi
