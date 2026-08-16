# Deployment Checklist — WhatsApp AI Platform

**Version:** 1.0  
**Date:** 2026-08-16

## Pre-Deployment

### Environment Setup

- [ ] Server provisioned (Hostinger VPS or equivalent)
- [ ] Docker and Docker Compose installed
- [ ] Domain configured with DNS pointing to server
- [ ] SSL certificate provisioned (Let's Encrypt)

### Configuration

- [ ] Copy `deploy/.env.production.example` to `.env`
- [ ] Generate encryption key: `openssl rand -base64 32`
- [ ] Set MySQL root password (strong, unique)
- [ ] Set Meta Verify Token and App Secret
- [ ] Configure CORS allowed origins
- [ ] Set rate limiting values

### Secrets

- [ ] Encryption key stored securely
- [ ] Meta credentials configured
- [ ] No secrets in source code or logs

## Deployment Steps

### 1. Build and Start Services

```bash
# Build images
docker compose build

# Start MySQL
docker compose up -d mysql

# Wait for MySQL health check
docker compose ps mysql

# Run migrations
docker compose run --rm api dotnet ef database update

# Seed subscription plans
docker compose up -d api

# Start all services
docker compose --profile frontend --profile production up -d
```

### 2. Verify Services

- [ ] MySQL healthy: `docker compose ps mysql`
- [ ] API responding: `curl https://yourdomain.com/health/live`
- [ ] Frontend accessible: `https://yourdomain.com`
- [ ] Nginx proxying correctly

### 3. Smoke Tests

- [ ] Create admin user (via bootstrap config)
- [ ] Login as PlatformAdmin
- [ ] Create test tenant with BOT plan
- [ ] Create test tenant with IA+BOT plan
- [ ] Activate tenant via invitation link
- [ ] Verify `/api/auth/me` returns correct data
- [ ] Test WhatsApp integration (if configured)
- [ ] Test AI integration (if configured)

## Post-Deployment

### Monitoring

- [ ] Health checks responding
- [ ] Logs being collected
- [ ] Error tracking configured
- [ ] Backup script scheduled (cron)

### Documentation

- [ ] Update DNS records documented
- [ ] Server access credentials secured
- [ ] Runbook accessible to team
- [ ] Incident register created

### Backup Verification

- [ ] Run manual backup: `./deploy/backup.sh`
- [ ] Verify backup file created
- [ ] Test restore on staging (if available)

## Rollback Plan

### If deployment fails:

1. Stop services: `docker compose down`
2. Restore database: `./deploy/restore.sh <backup_file>`
3. Revert to previous image: `git checkout <previous_tag>`
4. Redeploy: `docker compose up -d`

### If critical issue found:

1. Switch all conversations to Human mode (disable AI)
2. Notify affected tenants
3. Investigate and fix
4. Redeploy

## Sign-off

- [ ] Technical lead approved
- [ ] Security review completed
- [ ] LGPD compliance verified
- [ ] Backup/restore tested
- [ ] Monitoring configured
- [ ] Documentation updated

**Deployed by:** _________________  
**Date:** _________________  
**Version:** _________________
