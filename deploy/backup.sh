#!/bin/bash
# WhatsApp AI Platform - PostgreSQL Backup Script
# Run daily via cron: 0 2 * * * /path/to/backup.sh

set -e

# Configuration
BACKUP_DIR="/var/backups/whatsappai"
RETENTION_DAYS=7
POSTGRES_CONTAINER="whatsapp-ai-postgres-1"
POSTGRES_DB="${POSTGRES_DB:-whatsappai}"
POSTGRES_USER="${POSTGRES_USER:-whatsappai}"

# Create backup directory
mkdir -p "$BACKUP_DIR"

# Generate timestamp
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/backup_${TIMESTAMP}.dump"

# Create backup
echo "[$(date)] Starting backup..."
docker exec "$POSTGRES_CONTAINER" pg_dump \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    --format=custom > "$BACKUP_FILE"

# Verify backup
if [ -s "$BACKUP_FILE" ]; then
    echo "[$(date)] Backup created: $BACKUP_FILE ($(du -h "$BACKUP_FILE" | cut -f1))"
else
    echo "[$(date)] ERROR: Backup file is empty!"
    exit 1
fi

# Clean old backups
echo "[$(date)] Cleaning backups older than $RETENTION_DAYS days..."
find "$BACKUP_DIR" -name "backup_*.dump" -mtime +$RETENTION_DAYS -delete

# List recent backups
echo "[$(date)] Recent backups:"
ls -lh "$BACKUP_DIR"/backup_*.dump | tail -5

echo "[$(date)] Backup completed successfully."
