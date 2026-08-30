# Threat Model — WhatsApp AI Manager

**Version:** 1.0  
**Date:** 2026-08-14

## Assets

| Asset | Sensitivity | Location |
|-------|-------------|----------|
| WhatsApp access tokens | Critical | `ISecretStore` (encrypted) |
| WhatsApp Web/QR session | Critical | `ISecretStore` (encrypted) |
| OpenAI API keys | Critical | `ISecretStore` (encrypted) |
| User credentials | High | `users.password_hash` |
| Conversation content | High | `messages.content` |
| Tenant data | High | All tenant-scoped tables |
| Audit logs | Medium | `audit_logs` (immutable) |

## Threats and Mitigations

### T1: Cross-Tenant Data Access
- **Risk:** High
- **Mitigation:** `TenantId` on all entities; `ICurrentTenant` middleware; EF global query filters; architecture tests enforce boundaries.
- **Verification:** T013, T074, T075 tests.

### T2: Credential Exposure
- **Risk:** Critical
- **Mitigation:** `ISecretStore` with AES-256-CBC and Encrypt-then-MAC (HMAC-SHA256); tampered ciphertext is rejected before decryption, tokens are never returned to the browser, and `SanitizingEnricher` masks PII in logs.
- **Verification:** Security tests, log scanning.

### T3: Webhook Spoofing
- **Risk:** High
- **Mitigation:** HMAC-SHA256 signature verification with `app_secret`; rate limiting on webhook endpoints.
- **Verification:** T023, T027 tests.

### T4: Session Hijacking
- **Risk:** High
- **Mitigation:** HttpOnly/Secure/SameSite cookies; security stamp invalidation; CSRF protection.
- **Verification:** T011, T017 tests.

### T5: Prompt Injection
- **Risk:** Medium
- **Mitigation:** Backend validates AI decisions before sending; `BehaviorPolicy` sanitizes; no direct Meta API access from AI.
- **Verification:** T056, T058 tests.

### T6: Denial of Service
- **Risk:** Medium
- **Mitigation:** Rate limiting (100 req/min default, 500 for webhooks, 20 for auth); request size limits.
- **Verification:** Load tests (T083).

### T7: Instabilidade ou bloqueio de sessão QR
- **Risk:** High
- **Mitigation:** sessões Baileys isoladas por tenant e linha, segredos da ponte, reconexão controlada, observabilidade e aceite explícito do tenant.
- **Verification:** testes de integração do canal QR e runbook operacional.

## Residual Risks

- **Meta Cloud API outage:** linhas Cloud degradam graciosamente; linhas QR já conectadas continuam no canal Baileys independente.
- **Baileys/WhatsApp Web outage or account action:** linhas QR degradam graciosamente e podem exigir nova autenticação por QR.
- **OpenAI API outage:** Circuit breaker prevents cascading failures; handoff to human.
- **Database compromise:** Encrypted secrets require separate key; conversation content is plaintext in DB (acceptable for MVP).
