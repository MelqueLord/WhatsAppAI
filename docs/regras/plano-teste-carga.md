# Load Testing Plan

**Version:** 1.0  
**Date:** 2026-08-16

## Objectives (NFR-001, NFR-002, NFR-003, NFR-007)

| Metric | Target | Test Method |
|---|---|---|
| Webhook p95 | < 1s | 1000 concurrent webhooks |
| Inbox p95 | < 3s | 100 concurrent users |
| AI p95 | < 10s | 100 AI decisions |
| Capacity | 50 tenants, 100 concurrent | Sustained load |

## Test Scenarios

### Scenario 1: Webhook Ingestion

**Goal:** Validate NFR-001 (webhook p95 < 1s)

```
- Send 1000 webhooks in 1 minute
- Each webhook creates contact + conversation + message
- Measure time from POST to 200 response
- Verify idempotency (duplicate webhooks)
```

**Expected Results:**
- p50 < 200ms
- p95 < 1000ms
- p99 < 2000ms
- Zero data loss

### Scenario 2: Inbox Real-time

**Goal:** Validate NFR-002 (inbox p95 < 3s)

```
- 100 concurrent SignalR connections
- Each connection subscribes to tenant group
- Send 100 messages across 10 tenants
- Measure time from message creation to SignalR notification
```

**Expected Results:**
- p50 < 500ms
- p95 < 3000ms
- Zero missed notifications

### Scenario 3: AI Processing

**Goal:** Validate NFR-003 (AI p95 < 10s)

```
- Send 100 inbound messages to AI-enabled conversations
- Measure full pipeline: message → AI decision → outbox
- Include OpenAI API latency
- Separate: queue time, processing time, API time
```

**Expected Results:**
- Total p95 < 10s
- Queue time p95 < 2s
- API time p95 < 8s

### Scenario 4: Capacity Test

**Goal:** Validate NFR-007 (50 tenants, 100 concurrent)

```
- Create 50 tenants with realistic data
- 100 concurrent users performing:
  - View inbox
  - Send messages
  - Switch modes
  - View usage
- Sustained for 30 minutes
```

**Expected Results:**
- Zero errors
- Response time within targets
- Database connections stable
- Memory usage stable

## Tools

- **HTTP Load:** k6 or Apache Bench
- **SignalR:** Custom WebSocket client
- **Monitoring:** Grafana + Prometheus

## Reproducible runner

The read-only HTTP scenarios can be executed without additional dependencies:

```bash
PERF_BASE_URL=https://staging.example.com \
PERF_SCENARIO=health \
PERF_REQUESTS=1000 \
PERF_CONCURRENCY=50 \
node scripts/load-test.mjs
```

Use `PERF_SCENARIO=inbox` with `STAGING_EMAIL` and `STAGING_PASSWORD` to exercise
authenticated inbox reads. The workflow `.github/workflows/staging-performance.yml`
provides the same run manually in the staging environment and publishes p50/p95/p99,
throughput and HTTP errors to the job summary. It does not send WhatsApp or AI
messages.

## Test Data

```json
{
  "tenants": 50,
  "contacts_per_tenant": 100,
  "conversations_per_contact": 2,
  "messages_per_conversation": 20,
  "concurrent_users": 100
}
```

## Execution

### Prerequisites

1. Clean database with seed data
2. All workers running
3. Monitoring dashboards open
4. Error tracking enabled

### Steps

1. Seed test data
2. Run webhook scenario (5 min)
3. Run inbox scenario (5 min)
4. Run AI scenario (5 min)
5. Run capacity scenario (30 min)
6. Collect results
7. Generate report

### Report Template

```markdown
# Load Test Report - [Date]

## Environment
- API: [version]
- Database: PostgreSQL
- Workers: [count]

## Results

| Scenario | Target | Actual | Status |
|---|---|---|---|
| Webhook p95 | < 1s | [X]ms | ✅/❌ |
| Inbox p95 | < 3s | [X]ms | ✅/❌ |
| AI p95 | < 10s | [X]s | ✅/❌ |
| Capacity | 50T/100U | [X]T/[X]U | ✅/❌ |

## Issues Found
- [List any issues]

## Recommendations
- [List recommendations]
```

## Success Criteria

All targets must be met for production release:
- [ ] Webhook p95 < 1s
- [ ] Inbox p95 < 3s
- [ ] AI p95 < 10s
- [ ] 50 tenants / 100 concurrent users stable
- [ ] Zero data loss
- [ ] Zero unhandled errors
