# Disaster Recovery Runbook

**Version:** 1.0  
**Date:** 2026-08-14

## Backup Schedule

- **Database:** Automated daily backups with WAL archiving
- **Retention:** 30 days point-in-time recovery
- **Secrets:** Managed by vault service (automatic replication)

## Recovery Procedures

### Database Restore

1. Identify restore point (≤24h before incident)
2. Restore database from backup
3. Verify data integrity: check latest conversation/message timestamps
4. Run smoke tests: create test tenant, verify auth, send test message
5. Target: ≤4 hours from declaration to smoke test pass

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
