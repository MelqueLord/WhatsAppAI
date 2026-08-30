# Incident Response

## Ownership

The staging/production environment must define these GitHub environment variables:

- `ONCALL_PRIMARY`: first responder;
- `ONCALL_SECONDARY`: backup responder;
- `INCIDENT_CHANNEL`: approved incident channel or ticket queue.

The scheduled `Staging availability monitor` workflow checks `/health/live` and
`/health/ready` every five minutes. A failed run is the detection signal; when
`STAGING_ALERT_WEBHOOK_URL` is configured it also sends a generic alert without
credentials, tokens, tenant data or message content.

## Response

1. Primary on-call acknowledges the alert and records the correlation ID/time.
2. Check readiness, centralized errors/traces and database connectivity.
3. If messaging or AI is unsafe, disable AI or switch affected conversations to
   human mode while preserving Inbox/Outbox.
4. Roll back the application only after checking migration compatibility.
5. Verify liveness, readiness and the staging smoke workflow before closing.
6. Record impact, timeline, cause, mitigation and follow-up owner in the incident
   register.
