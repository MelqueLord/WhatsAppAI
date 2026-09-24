# ADR-0006: Hosting, Database and Secret Management

**Status:** Superseded by ADR-0008 — 2026-08-26

## Context

The platform needs production hosting, a managed PostgreSQL database and protected secrets. The constitution requires secrets to pass through `ISecretStore` and never persist in plaintext.

## Decision

**Hosting:** Hostinger VPS with Docker Compose (API, frontend, worker and Nginx).

**Database:** The provider and schema strategy are defined by ADR-0008: PostgreSQL via Npgsql, Supabase for managed environments and the official PostgreSQL image for self-hosting.

**Secrets:** Environment variables for development; hosting-managed secrets or a file-based vault for production. The `ISecretStore` abstraction allows swapping implementations without changing application code.

**Backup:** Automated daily PostgreSQL backups with point-in-time recovery ≤24h via external backup script.

**Reverse proxy:** Nginx with HTTPS (Let's Encrypt) as the entry point. The database port is never exposed to the internet.

## Consequences

- Production secrets are managed by environment variables or a file-based vault.
- `ISecretStore` has two implementations: `SecretStore` (local/AES) and a production implementation for the hosting environment.
- No secrets in source code, CI/CD logs, or application logs.
- Restore runbook must be tested quarterly.

## Alternatives Considered

- **Managed PostgreSQL:** Used for environments where Supabase is not available; the application contract remains unchanged.
- **Azure PaaS:** Higher cost and complexity for MVP; Hostinger VPS sufficient for the initial capacity.
