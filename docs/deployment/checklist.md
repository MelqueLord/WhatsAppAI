# Deployment Checklist — WhatsApp AI Platform

> Antes deste checklist, concluir `specs/000-platform/production-readiness-plan.md` e obter `GO` em `specs/000-platform/contracts/production-readiness-gates.md`.

**Version:** 1.0  
**Date:** 2026-08-16

## Atualização de status (2026-08-21)

- [x] P0 de hardening implementado em código/configuração (cookies/CSRF, segredos/versionamento, Compose/Nginx, migration bundle).
- [ ] Validação operacional P0 pendente em host com Docker/TLS (`docker compose config`, `nginx -t`, smoke HTTPS).
- [ ] P1 de qualidade pendente (3 testes .NET, 23 erros de lint, 1 teste frontend).

## Pre-Deployment

### Environment Setup

- [ ] Server provisioned (Hostinger VPS or equivalent)
- [ ] Docker and Docker Compose installed
- [ ] Domain configured with DNS pointing to server
- [ ] SSL certificate provisioned (Let's Encrypt)

### Configuration

- [ ] Copy `deploy/.env.production.example` to `.env`
- [ ] Generate encryption key: `openssl rand -base64 32`
- [ ] Set `BootstrapAdmin__Email` and a unique `BootstrapAdmin__Password` (at least 12 characters with upper/lowercase, number and symbol); do not commit either value
- [ ] Set PostgreSQL password (strong, unique)
- [ ] Set Meta Verify Token and App Secret
- [ ] Set DOMAIN for Nginx template rendering

### Secrets

- [ ] Encryption key stored securely
- [ ] Meta credentials configured
- [ ] No secrets in source code or logs

## Deployment Steps

### 1. Build and Start Services

```bash
# Build images
docker compose build

# Start PostgreSQL
docker compose up -d postgres

# Wait for PostgreSQL health check
docker compose ps postgres

# Run migrations
docker compose run --rm migrate

# Seed subscription plans
docker compose up -d api

# Start all services
docker compose --profile production up -d
```

### 2. Verify Services

- [ ] PostgreSQL healthy: `docker compose ps postgres`
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

For a reproducible validation against real staging integrations, run the manual
GitHub Actions workflow `Staging smoke` with the `staging` environment configured:

- `STAGING_BASE_URL` and `STAGING_QR_LINE_NUMBER` as environment variables;
- `STAGING_EMAIL` and `STAGING_PASSWORD` as environment secrets.

The workflow validates liveness/readiness, authenticated tenant context, the AI
provider connection, WhatsApp Cloud API, an existing QR session, and a real
SignalR WebSocket connection. It prints only pass/fail checks and never logs
credentials or message content. Attach the workflow run URL to the release
evidence before approving the production gate.

## Post-Deployment

### Monitoring

- [ ] Health checks responding
- [ ] Logs being collected
- [ ] Error tracking configured
- [ ] Backup script scheduled (cron)
- [ ] Configure `STAGING_BASE_URL` and enable the scheduled `Staging availability monitor` workflow
- [ ] Configure `STAGING_ALERT_WEBHOOK_URL` (optional) and verify a test alert reaches the incident channel
- [ ] Define `ONCALL_PRIMARY`, `ONCALL_SECONDARY` and `INCIDENT_CHANNEL` in the staging environment
- [ ] Configure `OpenTelemetry__Endpoint` for the centralized metrics/traces/error collector

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
