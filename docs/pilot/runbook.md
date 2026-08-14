# Pilot Runbook — WhatsApp AI Manager

**Version:** 1.0  
**Date:** 2026-08-14

## SC-001: Tenant Provisioning (Target: ≤60 min)

1. PlatformAdmin creates tenant via `POST /api/admin/tenants`
2. Copy invitation link from response (displayed once)
3. Send link to TenantOwner via secure channel
4. TenantOwner opens link, sets password → account activated
5. TenantOwner invites Operator via `POST /api/operators`
6. Operator activates via invitation link
7. Verify `GET /api/auth/me` returns correct tenant/role for both

**Expected time:** 15-30 minutes

## SC-002: Webhook Ingestion

1. Configure WhatsApp account via `POST /api/integrations/whatsapp`
2. Send test message from WhatsApp
3. Verify webhook received and persisted (check `GET /api/webhook-events`)
4. Verify conversation created in inbox

**Target:** 1,000 events without loss or duplication

## SC-003: Real-Time Inbox

1. Open inbox UI as Operator
2. Send message from WhatsApp
3. Verify message appears in inbox within 3 seconds (p95)
4. Verify conversation list updates

## SC-004: AI Response

1. Configure AI provider via `POST /api/integrations/ai`
2. Send message from WhatsApp in Automatic mode
3. Verify AI response sent back within 30 seconds
4. Verify handoff triggers on low confidence

## SC-005: Tenant Isolation

1. Create two tenants with separate owners
2. Verify Tenant A cannot access Tenant B's conversations, contacts, or settings
3. Verify webhook events are scoped to correct tenant

## SC-006: Availability Monitoring

1. Health endpoints respond: `/health/live`, `/health/ready`
2. Correlation ID present in all responses
3. Logs sanitized (no secrets, no PII)
4. SLI calculation: uptime = (total - downtime - maintenance) / (total - maintenance)

## Incident Response

1. **Detection:** Health check failure, log alert, user report
2. **Triage:** Check `/health/ready`, database connectivity, external API status
3. **Mitigation:** Restart service, failover to backup, disable AI (switch to Human mode)
4. **Resolution:** Fix root cause, deploy fix, verify
5. **Post-mortem:** Document in `docs/pilot/incidents/`

## Rollback

- Database: `dotnet ef database rollback` to previous migration
- Application: redeploy previous container image
- AI: disable via `POST /api/integrations/ai` (deactivate credential)
