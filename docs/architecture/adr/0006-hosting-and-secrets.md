# ADR-0006: Hosting and Secret Management

**Status:** Proposed
**Date:** 2026-08-14

## Context

The platform needs production hosting and a managed secret store. The constitution requires secrets to pass through `ISecretStore` and never persist in plaintext. Current development uses SQLite + AES-encrypted local secrets.

## Decision

**Hosting:** Linux App Service (Azure) or equivalent PaaS with PostgreSQL managed instance.

**Secrets:** Environment variables for development; Azure Key Vault (or equivalent) for production. The `ISecretStore` abstraction allows swapping implementations without changing application code.

**Backup:** Automated daily PostgreSQL backups with point-in-time recovery ≤24h.

## Consequences

- Production secrets are managed by the cloud provider's vault service.
- `ISecretStore` has two implementations: `SecretStore` (local/AES) and `KeyVaultSecretStore` (managed).
- No secrets in source code, CI/CD logs, or application logs.
- Restore runbook must be tested quarterly.

## Alternatives Considered

- **Self-managed HashiCorp Vault:** Higher operational overhead for MVP.
- **AWS Secrets Manager:** Equivalent; chosen based on team familiarity with Azure.
