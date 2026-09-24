# Threat Model — WhatsApp AI Manager

**Version:** 1.1
**Date:** 2026-09-13
**Correção aberta:** [segurança e prontidão](../specs/producao/seguranca-e-prontidao.md)

## Assets

| Asset | Sensitivity | Location |
|---|---|---|
| WhatsApp access tokens | Critical | `ISecretStore` (encrypted) |
| WhatsApp Web/QR session | Critical | `ISecretStore` (encrypted) |
| OpenAI API keys | Critical | `ISecretStore` (encrypted) |
| User credentials | High | `users.password_hash` |
| Conversation content | High | `messages.content` |
| Tenant data | High | All tenant-scoped tables |
| Audit logs | Medium | `audit_logs` (immutable) |

## Threats and mitigations

### T1: Cross-tenant data access

- **Risk:** High
- **Required control:** `TenantId` on all business entities, tenant resolution in the execution context, enforced query predicates and architecture/integration tests.
- **Current gap:** global filters are present, but bypasses via `IgnoreQueryFilters()` and repository contracts require an explicit inventory and hardening.
- **Verification:** CR-003, T260–T261; tests must prove isolation for reads, writes, deletion and SignalR.

### T2: Credential exposure

- **Risk:** Critical
- **Required control:** `ISecretStore` with authenticated encryption; credentials never returned to browser code; sanitation before every log sink.
- **Current gap:** browser code persists/logs an access token and the existing sanitizer does not establish a global, tested barrier for structured properties and exceptions.
- **Verification:** CR-001, CR-004, T255–T259; sentinel tests and source review.

### T3: Webhook spoofing

- **Risk:** High
- **Required control:** HMAC-SHA256 signature verification with `app_secret`, idempotency and rate limiting on public provider webhooks.
- **Verification:** T023, T027 and regression tests for invalid source/signature.

### T4: Session hijacking or revoked access

- **Risk:** High
- **Required control:** `HttpOnly`, `Secure`, `SameSite=Lax` session cookie, antiforgery protection and current user/membership/security-stamp validation for every accepted credential.
- **Current gap:** JWT supplied to the browser can remain valid independently of the cookie validation path.
- **Verification:** CR-001, CR-002, T255–T257, including SignalR.

### T5: Prompt injection

- **Risk:** Medium
- **Required control:** backend validates AI decisions before sending; behavior policies sanitize; AI has no direct Meta API access.
- **Verification:** T056, T058 tests.

### T6: Denial of service

- **Risk:** Medium
- **Required control:** environment-configured limits, request size limits and measured health/readiness.
- **Current gap:** current limits are coded rather than consumed from environment configuration.
- **Verification:** CR-005, CR-009, T258 and T266.

### T7: QR session compromise or excess retention

- **Risk:** High
- **Required control:** sessions isolated by tenant and line, internal service identity, lease verification on every mutation, controlled reconnect and minimal protected state.
- **Current gap:** session commands rely on a global secret and an independent conversation snapshot is retained by the bridge.
- **Verification:** CR-006, CR-007, ADR-0015 and T262–T264.

## Residual risks

- **Meta Cloud API outage:** Cloud lines degrade gracefully; QR lines already connected remain independent.
- **Baileys/WhatsApp Web outage or account action:** QR lines degrade gracefully and can require renewed authentication.
- **OpenAI API outage:** circuit breaker avoids cascading failures and hands off to human service.
- **Database compromise:** encrypted secrets depend on a separate key; conversation content is plaintext in the operational database for the MVP.

No threat above is considered closed merely because a control exists in source. Closure requires the acceptance evidence recorded in the corresponding task.
