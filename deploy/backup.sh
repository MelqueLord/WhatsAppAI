#!/bin/bash
# WhatsApp AI Platform - MySQL Backup Script
# Run daily via cron: 0 2 * * * /path/to/backup.sh

set -e

# Configuration
BACKUP_DIR="/var/backups/whatsappai"
RETENTION_DAYS=7
MYSQL_CONTAINER="whatsapp-ai-mysql-1"
MYSQL_DATABASE="whatsappai_prod"
MYSQL_ROOT_PASSWORD="${MYSQL_ROOT_PASSWORD}"

# Create backup directory
mkdir -p "$BACKUP_DIR"

# Generate timestamp
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/backup_${TIMESTAMP}.sql.gz"

# Create backup
echo "[$(date)] Starting backup..."
docker exec "$MYSQL_CONTAINER" mysqldump \
    -u root \
    -p"$MYSQL_ROOT_PASSWORD" \
    --single-transaction \
    --routines \
    --triggers \
    "$MYSQL_DATABASE" | gzip > "$BACKUP_FILE"

# Verify backup
if [ -s "$BACKUP_FILE" ]; then
    echo "[$(date)] Backup created: $BACKUP_FILE ($(du -h "$BACKUP_FILE" | cut -f1))"
else
    echo "[$(date)] ERROR: Backup file is empty!"
    exit 1
fi

# Clean old backups
echo "[$(date)] Cleaning backups older than $RETENTION_DAYS days..."
find "$BACKUP_DIR" -name "backup_*.sql.gz" -mtime +$RETENTION_DAYS -delete

# List recent backups
echo "[$(date)] Recent backups:"
ls -lh "$BACKUP_DIR"/backup_*.sql.gz | tail -5

echo "[$(date)] Backup completed successfully."
