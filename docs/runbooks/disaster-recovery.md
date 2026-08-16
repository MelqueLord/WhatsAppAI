# Disaster Recovery Runbook

**Version:** 1.0  
**Date:** 2026-08-14

## Backup Schedule

- **Database:** Automated daily MySQL backups via `deploy/backup.sh`
- **Retention:** 7 days (configurable via RETENTION_DAYS)
- **Secrets:** AES-256 encrypted in database; encryption key in environment variable
- **Backup location:** `/var/backups/whatsappai/`

## Recovery Procedures

### Database Restore

1. Identify restore point (≤24h before incident)
2. Stop application: `docker compose stop api worker`
3. Run restore: `./deploy/restore.sh /var/backups/whatsappai/backup_YYYYMMDD_HHMMSS.sql.gz`
4. Restart application: `docker compose up -d api worker`
5. Verify data integrity: check latest conversation/message timestamps
6. Run smoke tests: create test tenant, verify auth, send test message
7. Target: ≤4 hours from declaration to smoke test pass

### Application Recovery

1. Redeploy last known good container image
2. Verify health endpoints respond
3. Check worker processes (webhook, outbox, AI, retention)
4. Monitor error rates for 30 minutes

### Full Site Recovery

1. Restore database (see above)
2. Deploy application to new infrastructure
3. Update DNS/load balancer
4. Verify webhook delivery (Meta retries for 24h)
5. Process any pending outbox messages

## Testing

- **Frequency:** Quarterly
- **Scope:** Full database restore + smoke test
- **Evidence:** Record timestamps, document any issues
- **Owner:** Engineering team

## Contacts

- **Infrastructure:** [TBD]
- **Database:** [TBD]
- **Security:** [TBD]
