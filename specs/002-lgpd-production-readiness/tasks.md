# Tasks: LGPD Production Readiness

**Input**: Design documents from `specs/002-lgpd-production-readiness/`

**Tests**: Unit, integration, tenant isolation, migration and release build are required.

## Phase 1: Setup

- [X] T001 Record the privacy architecture decision in `docs/architecture/adr/0008-lgpd-operational-controls.md`

## Phase 2: Foundational

- [X] T002 Create privacy domain entities and transitions in `src/WhatsAppAI.Domain/Privacy/ProcessingPurpose.cs`, `ConsentEvidence.cs`, and `DataSubjectRequest.cs`
- [X] T003 Add EF mappings, DbSets and tenant filters in `src/WhatsAppAI.Infrastructure/Persistence/AppDbContext.cs` and `src/WhatsAppAI.Infrastructure/Persistence/Configurations/*Privacy*Configuration.cs`
- [X] T004 [P] Add domain tests in `tests/WhatsAppAI.UnitTests/Privacy/PrivacyDomainTests.cs`

## Phase 3: User Story 1 — Legal basis and consent (P1)

**Goal**: Record purposes and consent evidence without requiring blanket consent.

**Independent Test**: A TenantOwner can create both consent and non-consent purposes; evidence is accepted only for consent and remains tenant-scoped.

- [X] T005 [US1] Implement tenant-owner purpose and consent endpoints in `src/WhatsAppAI.WebApi/Privacy/PrivacyEndpoints.cs`
- [X] T006 [US1] Add purpose/consent tenant-isolation integration tests in `tests/WhatsAppAI.IntegrationTests/Privacy/PrivacyEndpointsTests.cs`

## Phase 4: User Story 2 — Data-subject rights (P1)

**Goal**: Open, export, deny and erase requests safely.

**Independent Test**: Export/anonymization affects one contact in one tenant, is idempotent, and leaves a same-number contact in another tenant unchanged.

- [X] T007 [US2] Add privacy redaction operations to `src/WhatsAppAI.Domain/Messaging/Contact.cs` and `src/WhatsAppAI.Domain/Messaging/Message.cs`
- [X] T008 [US2] Implement request, export, deny and transactional erasure endpoints in `src/WhatsAppAI.WebApi/Privacy/PrivacyEndpoints.cs`
- [X] T009 [US2] Add export/erasure/idempotency/isolation tests in `tests/WhatsAppAI.IntegrationTests/Privacy/PrivacyEndpointsTests.cs`

## Phase 5: User Story 3 — Governance evidence (P2)

**Goal**: Publish configured privacy identity and keep governance evidence versioned.

**Independent Test**: Public notice returns only configured values and remains available with explicit incomplete status when they are absent.

- [X] T010 [P] [US3] Add public privacy notice configuration and endpoint in `src/WhatsAppAI.WebApi/Privacy/PrivacyEndpoints.cs` and `src/WhatsAppAI.WebApi/appsettings.json`
- [X] T011 [P] [US3] Create RIPD and controller/operator procedure in `docs/security/lgpd-ripd.md` and update `docs/security/lgpd-checklist.md`

## Phase 6: Database and release validation

- [X] T012 Generate reversible PostgreSQL migration and snapshot updates in `src/WhatsAppAI.Infrastructure.PostgreSqlMigrations/Migrations/`
- [ ] T013 Register endpoints, build Release, run privacy/unit/integration tests, validate no pending Npgsql model changes, review diff, and record remaining deployment inputs in `specs/002-lgpd-production-readiness/quickstart.md`. Partial validation completed on 2026-08-30: Release build, unit/architecture tests, frontend checks, EF model check, 67 integration tests, Docker migration bundle/idempotency, and Production API/worker startup passed; Data Protection persistence/encryption was validated with a temporary PFX; rollback/restore and operational release gates remain open.

## Dependencies & Execution Order

- T001 → T002/T004 → T003 → T005/T007 → T006/T008 → T009 → T010/T011 → T012 → T013.
- T004, T010 and T011 touch independent files and may proceed independently when their prerequisites are satisfied.

## Implementation Strategy

Deliver both P1 stories before governance publication. Keep all writes tenant-scoped and run export/erasure through the existing DbContext transaction. Do not introduce a public unauthenticated DSAR intake in this increment.
