#!/bin/bash
# WhatsApp AI Platform - MySQL Restore Script
# Usage: ./restore.sh <backup_file.sql.gz>

set -e

if [ -z "$1" ]; then
    echo "Usage: $0 <backup_file.sql.gz>"
    echo "Available backups:"
    ls -lh /var/backups/whatsappai/backup_*.sql.gz 2>/dev/null || echo "No backups found."
    exit 1
fi

BACKUP_FILE="$1"
MYSQL_CONTAINER="whatsapp-ai-mysql-1"
MYSQL_DATABASE="whatsappai_prod"
MYSQL_ROOT_PASSWORD="${MYSQL_ROOT_PASSWORD}"

if [ ! -f "$BACKUP_FILE" ]; then
    echo "ERROR: Backup file not found: $BACKUP_FILE"
    exit 1
fi

echo "WARNING: This will overwrite the current database!"
echo "Backup file: $BACKUP_FILE"
echo "Database: $MYSQL_DATABASE"
read -p "Continue? (y/N): " -n 1 -r
echo

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborted."
    exit 0
fi

echo "[$(date)] Starting restore..."

# Drop and recreate database
docker exec "$MYSQL_CONTAINER" mysql \
    -u root \
    -p"$MYSQL_ROOT_PASSWORD" \
    -e "DROP DATABASE IF EXISTS $MYSQL_DATABASE; CREATE DATABASE $MYSQL_DATABASE CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"

# Restore backup
gunzip -c "$BACKUP_FILE" | docker exec -i "$MYSQL_CONTAINER" mysql \
    -u root \
    -p"$MYSQL_ROOT_PASSWORD" \
    "$MYSQL_DATABASE"

echo "[$(date)] Restore completed successfully."
echo "[$(date)] Restart the application: docker compose restart api worker"
