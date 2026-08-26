# ADR-0006: Hosting, Database and Secret Management

**Status:** Superseded by ADR-0008 — 2026-08-26

## Context

The platform needs production hosting, a database provider and a managed secret store. The constitution requires secrets to pass through `ISecretStore` and never persist in plaintext. Current development uses SQLite with AES-encrypted local secrets.

## Decision

**Hosting:** Hostinger VPS with Docker Compose (API, frontend, worker, MySQL, Nginx).

**Database:** MySQL 8.4 LTS with `MySql.EntityFrameworkCore` provider. UTF-8 (`utf8mb4`) for text and emojis. UTC timestamps stored as `datetime(6)`. GUIDs stored as `char(36)`.

**Secrets:** Environment variables for development; Hostinger-managed secrets or file-based vault for production. The `ISecretStore` abstraction allows swapping implementations without changing application code.

**Backup:** Automated daily MySQL backups with point-in-time recovery ≤24h via external backup script.

**Reverse proxy:** Nginx with HTTPS (Let's Encrypt) as the entry point. MySQL port 3306 not exposed to the internet.

## Consequences

- MySQL replaces PostgreSQL throughout: EF Core provider, migrations, connection strings, Docker Compose, Testcontainers.
- `utf8mb4` character set supports full Unicode including emojis for WhatsApp messages.
- All timestamps use `datetime(6)` (microsecond precision) in UTC.
- Partial indexes (PostgreSQL `WHERE` clauses) are removed; MySQL does not support them.
- Production secrets are managed by environment variables or file-based vault.
- `ISecretStore` has two implementations: `SecretStore` (local/AES) and a production implementation for the hosting environment.
- No secrets in source code, CI/CD logs, or application logs.
- Restore runbook must be tested quarterly.

## Alternatives Considered

- **PostgreSQL 18:** Original choice; replaced by MySQL for Hostinger VPS compatibility and simpler hosting.
- **Azure PaaS:** Higher cost and complexity for MVP; Hostinger VPS sufficient for 50 tenants.
- **Managed MySQL (RDS/CloudSQL):** Overkill for MVP; VPS MySQL with external backup is sufficient.
