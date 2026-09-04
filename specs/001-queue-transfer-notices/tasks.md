# Tasks: Queue Transfer Notices

## Phase 1 — Foundation

- [X] T001 Add optional queue transfer notice model, mapping and reversible migration in `src/WhatsAppAI.Domain/Messaging/ServiceLine.cs`, `src/WhatsAppAI.Infrastructure/Persistence/Configurations/ServiceLineConfiguration.cs`, and `src/WhatsAppAI.Infrastructure/Migrations/`.

## Phase 2 — User Story 1: Configure and send queue notice

**Goal**: TenantOwner can configure a notice and automatic transfers use it once.

- [X] T002 [P] [US1] Add unit tests for notice normalization and resolution in `tests/WhatsAppAI.UnitTests/Automation/AiOrchestrationWorkerTests.cs`.
- [X] T003 [US1] Expose and validate `transferNotice` in `src/WhatsAppAI.WebApi/Queues/ServiceQueueEndpoints.cs`.
- [X] T004 [US1] Resolve a selected queue's notice in `src/WhatsAppAI.Infrastructure/Workers/AiOrchestrationWorker.cs`.
- [X] T005 [US1] Add the queue notice field to `apps/web/src/features/queues/QueuesPage.tsx`.

## Phase 3 — User Story 2: Preserve fallback behavior

- [X] T006 [US2] Test the queue-specific, tenant-wide and platform-default fallback order in `tests/WhatsAppAI.UnitTests/Automation/AiOrchestrationWorkerTests.cs`.

## Phase 4 — User Story 3: Tenant isolation

- [X] T007 [US3] Add tenant-isolation coverage for queue notice persistence and API behavior in `tests/WhatsAppAI.IntegrationTests/Messaging/`.

## Phase 5 — Validation

- [ ] T008 Validate migration, backend build, relevant tests and frontend build; update `specs/001-queue-transfer-notices/quickstart.md` if needed.

## Dependencies

`T001 → T003/T004/T005 → T006/T007 → T008`
