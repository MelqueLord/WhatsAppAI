# Observability Runbook

**Version:** 1.0  
**Date:** 2026-08-16

## Metrics

### Application Metrics (Serilog + OpenTelemetry)

| Metric | Description | Alert Threshold |
|---|---|---|
| `webhook_latency_p95` | Webhook processing latency | > 1s |
| `inbox_latency_p95` | Inbox message delivery latency | > 3s |
| `ai_latency_p95` | AI decision latency | > 10s |
| `outbox_queue_depth` | Pending outbox messages | > 100 |
| `webhook_queue_depth` | Pending webhook events | > 50 |
| `error_rate_5xx` | 5xx error rate | > 1% |

### SLI Calculation (NFR-004)

```
SLI = (Completed responses without 5xx or platform timeout) / (Total valid requests received)

- Exclude: Meta/OpenAI failures (separate dimension)
- Include: Maintenance windows
- Target: 99.5% monthly
```

### Business Metrics

| Metric | Description |
|---|---|
| `tenants_active` | Active tenants count |
| `messages_per_day` | Daily message volume |
| `ai_decisions_per_day` | Daily AI decisions |
| `handoff_rate` | AI-to-human handoff rate |

## Logging

### Correlation ID

Every request gets a correlation ID via `CorrelationIdMiddleware`:
- Header: `X-Correlation-Id`
- Propagated to all logs and external calls
- Used for request tracing across services

### Log Format

```json
{
  "timestamp": "2026-08-16T10:30:00.000Z",
  "level": "Information",
  "message": "Webhook processed",
  "correlationId": "abc-123",
  "tenantId": "tenant-456",
  "conversationId": "conv-789",
  "duration": 250,
  "statusCode": 200
}
```

### Sanitization

The `SanitizingEnricher` strips:
- API keys (`sk-...`)
- Tokens
- Phone numbers (masked: `+5511****9999`)
- Email addresses (masked: `j***@example.com`)

## Health Checks

### Endpoints

- `GET /health/live` — Liveness (process running)
- `GET /health/ready` — Readiness (DB connected, workers running)

### Checks

| Check | Description |
|---|---|
| `mysql` | Database connectivity |
| `worker-webhook` | Webhook processing worker |
| `worker-outbox` | Outbox processing worker |
| `worker-ai` | AI orchestration worker |
| `worker-retention` | Retention worker |

## Alerting

### Critical Alerts (Page immediately)

- Database connection failure
- 5xx error rate > 5%
- Webhook queue > 500
- All workers stopped

### Warning Alerts (Investigate within 1h)

- 5xx error rate > 1%
- Webhook queue > 100
- AI latency p95 > 10s
- Outbox queue > 200

## Dashboards

### Grafana Dashboard Panels

1. **Request Rate** — Requests/sec by endpoint
2. **Error Rate** — 4xx/5xx by endpoint
3. **Latency** — p50/p95/p99 by endpoint
4. **Queue Depths** — Webhook and Outbox queues
5. **AI Performance** — Decisions, latency, handoff rate
6. **Tenant Activity** — Active tenants, messages/day

## Troubleshooting

### High Webhook Queue

1. Check webhook worker logs
2. Verify MySQL connectivity
3. Check Meta API rate limits
4. Look for signature verification failures

### High AI Latency

1. Check OpenAI API status
2. Verify circuit breaker state
3. Check token usage limits
4. Review model configuration

### Missing Messages

1. Check correlation ID in logs
2. Verify tenant context
3. Check conversation mode
4. Review outbox status
