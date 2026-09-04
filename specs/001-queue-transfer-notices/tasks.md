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

## Phase 6 — User Story 4: Keep customers informed while waiting

- [X] T009 [US4] Add deterministic waiting-message resolution and automatic queue handling in `src/WhatsAppAI.Infrastructure/Workers/AiOrchestrationWorker.cs`.
- [X] T010 [US4] Preserve human-mode handoff behavior while allowing authorized keywords to move automatic conversations between queues.
- [X] T011 [US4] Add unit coverage for the waiting message and queue-state transitions in `tests/WhatsAppAI.UnitTests/Automation/`.
- [ ] T012 [US4] Validate duplicate-safe queue waiting outbox creation and update the quickstart flow.
- [X] T013 [US4] Keep every queue, including the human queue, automatic until an operator explicitly assumes the conversation; queue assignment alone must not switch mode.

## Phase 7 — User Story 5: Continue AI service while queued

- [X] T014 [US5] Add prompt coverage for the business-specific first-contact welcome guidance in `tests/WhatsAppAI.UnitTests/Automation/ContextAssemblerTests.cs`.
- [X] T015 [US5] Allow queued automatic messages to reach the AI, use the queue notice only for `no_action`, and preserve human handoff for out-of-scope decisions in `src/WhatsAppAI.Infrastructure/Workers/AiOrchestrationWorker.cs`.
- [X] T016 [US5] Clarify the business-specific AI welcome message in `apps/web/src/features/bot/BotConfigPage.tsx` and document the queued-AI validation flow in `specs/001-queue-transfer-notices/quickstart.md`.
- [X] T017 [US5] Enforce human handoff for AI decisions marked `out_of_scope` and cover the distinction from ambiguous human requests in `src/WhatsAppAI.Application/Automation/Policy/` and `tests/WhatsAppAI.UnitTests/Automation/`.

## Dependencies

`T001 → T003/T004/T005 → T006/T007 → T009/T010/T011/T013 → T014/T015/T016/T017 → T008/T012`
