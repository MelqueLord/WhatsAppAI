#!/bin/bash
# WhatsApp AI Platform - PostgreSQL Restore Script
# Usage: ./restore.sh <backup_file.dump>

set -e

if [ -z "$1" ]; then
    echo "Usage: $0 <backup_file.dump>"
    echo "Available backups:"
    ls -lh /var/backups/whatsappai/backup_*.dump 2>/dev/null || echo "No backups found."
    exit 1
fi

BACKUP_FILE="$1"
POSTGRES_CONTAINER="whatsapp-ai-postgres-1"
POSTGRES_DB="${POSTGRES_DB:-whatsappai}"
POSTGRES_USER="${POSTGRES_USER:-whatsappai}"

if [ ! -f "$BACKUP_FILE" ]; then
    echo "ERROR: Backup file not found: $BACKUP_FILE"
    exit 1
fi

echo "WARNING: This will overwrite the current database!"
echo "Backup file: $BACKUP_FILE"
echo "Database: $POSTGRES_DB"
read -p "Continue? (y/N): " -n 1 -r
echo

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborted."
    exit 0
fi

echo "[$(date)] Starting restore..."

docker exec "$POSTGRES_CONTAINER" dropdb --if-exists --force --username "$POSTGRES_USER" "$POSTGRES_DB"
docker exec "$POSTGRES_CONTAINER" createdb --username "$POSTGRES_USER" "$POSTGRES_DB"
docker exec -i "$POSTGRES_CONTAINER" pg_restore \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    --clean --if-exists < "$BACKUP_FILE"

echo "[$(date)] Restore completed successfully."
echo "[$(date)] Restart the application: docker compose restart api worker"
