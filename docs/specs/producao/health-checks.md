# Availability Runbook

**Version:** 1.1
**Date:** 2026-09-13

> **Estado atual:** a semântica descrita abaixo é obrigatória, mas a revisão A-002 encontrou readiness sem dependência marcada como `ready`. A correção CR-005/T258 permanece bloqueadora até que os testes de indisponibilidade de PostgreSQL e a evidência de staging sejam anexados.

## SLI Definition (NFR-004)

```
SLI = (Total minutes - Downtime minutes - Maintenance minutes) / (Total minutes - Maintenance minutes) × 100
```

- **Total minutes:** Calendar minutes in the month
- **Downtime:** Any period where `/health/ready` fails or error rate > 5%
- **Maintenance:** Scheduled maintenance windows (max 4h/month)

## SLI Calculation

Monthly report must include:
1. Total minutes in month
2. Sum of downtime minutes (with timestamps)
3. Sum of maintenance minutes (with approval)
4. Calculated SLI percentage
5. Target: 99.5% for MVP

## Dashboard Dimensions

- **Overall:** SLI for entire platform
- **By tenant:** Per-tenant availability
- **By component:** Webhook ingestion, inbox, AI response, Meta API, OpenAI API
- **By error type:** 5xx, timeout, external API failure

## Alerting

| Condition | Severity | Response |
|-----------|----------|----------|
| `/health/ready` fails | Critical | Immediate investigation |
| Error rate > 5% for 5 min | High | Investigate within 15 min |
| p95 latency > 3s | Medium | Investigate within 1 hour |
| AI circuit breaker open | Medium | Check OpenAI status |

## Tools

- Health endpoints: `/health/live`, `/health/ready`
- Correlation ID: `X-Correlation-ID` header
- Logs: Structured JSON with Serilog
- Metrics: OpenTelemetry → configured exporter
