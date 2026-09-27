# Tasks: Cadastro de templates na Meta

## Phase 1 — Setup

- [X] T001 Verify repository ignore rules and centralize the Meta Graph version in `src/WhatsAppAI.Infrastructure/Meta/MetaGraphOptions.cs` and `src/WhatsAppAI.WebApi/appsettings.json`
- [X] T002 [P] Add reusable template validation and result contracts to `src/WhatsAppAI.Application/Integrations/IWhatsAppClient.cs` and `src/WhatsAppAI.Application/Integrations/WhatsAppTemplateValidator.cs`

## Phase 2 — Foundational data model

- [X] T003 Add WABA, template, and durable submission domain entities in `src/WhatsAppAI.Domain/Integrations/WhatsAppBusinessAccount.cs`, `src/WhatsAppAI.Domain/Integrations/WhatsAppMessageTemplate.cs`, and `src/WhatsAppAI.Domain/Integrations/WhatsAppTemplateSubmission.cs`
- [X] T004 Link official lines to their WABA aggregate in `src/WhatsAppAI.Domain/Integrations/WhatsAppAccount.cs`
- [X] T005 Add tenant-aware repository ports in `src/WhatsAppAI.Application/Abstractions/IWhatsAppTemplateRepository.cs`
- [X] T006 Configure EF mappings, query filters, and repositories in `src/WhatsAppAI.Infrastructure/Persistence/`
- [X] T007 Create the reversible PostgreSQL migration and snapshot changes in `src/WhatsAppAI.Infrastructure/Migrations/`
- [X] T008 [P] Add domain and persistence tests in `tests/WhatsAppAI.UnitTests/Integrations/WhatsAppMessageTemplateTests.cs` and `tests/WhatsAppAI.IntegrationTests/Persistence/WhatsAppTemplatePersistenceTests.cs`

## Phase 3 — User Story 1: cadastrar template (P1)

**Independent test:** a TenantOwner submits a valid textual template for an active official line, receives `202`, and repeated idempotent requests do not create duplicate remote posts.

- [X] T009 [US1] Extend the Meta adapter with typed template creation and enriched listing in `src/WhatsAppAI.Infrastructure/Meta/WhatsAppClient.cs`
- [X] T010 [US1] Implement durable claim, retry, and reconciliation processing in `src/WhatsAppAI.Infrastructure/Workers/WhatsAppTemplateSubmissionWorker.cs`
- [X] T011 [US1] Register the submission worker and repositories in `src/WhatsAppAI.Infrastructure/Workers/WorkerServiceCollectionExtensions.cs` and `src/WhatsAppAI.Infrastructure/Persistence/PersistenceServiceCollectionExtensions.cs`
- [X] T012 [US1] Add TenantOwner create endpoint, validation, idempotency, and sanitized audit in `src/WhatsAppAI.WebApi/Integrations/WhatsAppTemplateEndpoints.cs`
- [X] T013 [P] [US1] Add Meta contract and worker tests in `tests/WhatsAppAI.UnitTests/Meta/WhatsAppTemplateClientTests.cs` and `tests/WhatsAppAI.UnitTests/Workers/WhatsAppTemplateSubmissionWorkerTests.cs`
- [X] T014 [US1] Add authorization, idempotency, and tenant-isolation endpoint tests in `tests/WhatsAppAI.IntegrationTests/Integrations/WhatsAppTemplateEndpointsTests.cs`

## Phase 4 — User Story 2: acompanhar análise (P1)

**Independent test:** authenticated template webhooks route by WABA, deduplicate fragments, and update only the owning tenant; explicit sync converges external changes.

- [X] T015 [US2] Evolve webhook routing metadata in `src/WhatsAppAI.Domain/Messaging/WebhookEvent.cs`, its EF configuration, and migration
- [X] T016 [US2] Persist all template webhook fragments by WABA in `src/WhatsAppAI.WebApi/Webhooks/WebhookEndpoints.cs`
- [X] T017 [US2] Apply status/category/component events and catalog synchronization in `src/WhatsAppAI.Infrastructure/Workers/WebhookProcessingWorker.cs` and `src/WhatsAppAI.Infrastructure/Workers/WhatsAppTemplateCatalogSyncService.cs`
- [X] T018 [US2] Add manual synchronization endpoint in `src/WhatsAppAI.WebApi/Integrations/WhatsAppTemplateEndpoints.cs`
- [X] T019 [US2] Add multi-entry, duplicate, unknown-WABA, and status transition coverage in `tests/WhatsAppAI.IntegrationTests/Webhooks/WebhookTests.cs`

## Phase 5 — User Story 3: consultar catálogo (P2)

**Independent test:** a TenantOwner lists separate name/language variants for the selected line's WABA with review state and inbox/broadcast compatibility.

- [X] T020 [US3] Add paginated tenant-scoped catalog query to `src/WhatsAppAI.WebApi/Integrations/WhatsAppTemplateEndpoints.cs`
- [X] T021 [US3] Add frontend API types and operations in `apps/web/src/lib/api.ts`
- [X] T022 [US3] Create the TenantOwner template form and catalog page in `apps/web/src/features/integrations/whatsapp/templates/WhatsAppTemplatesPage.tsx`
- [X] T023 [US3] Register owner-only route and navigation in `apps/web/src/App.tsx` and `apps/web/src/components/Sidebar.tsx`
- [X] T024 [P] [US3] Add page, route, and navigation tests in `apps/web/src/features/integrations/whatsapp/templates/WhatsAppTemplatesPage.test.tsx`, `apps/web/src/App.test.tsx`, and `apps/web/src/components/Sidebar.test.tsx`

## Phase 6 — Polish and cross-cutting validation

- [X] T025 Review send-flow regression and sanitized observability in `src/WhatsAppAI.WebApi/` and `src/WhatsAppAI.Infrastructure/`
- [ ] T026 Run format, Release build, .NET tests, frontend lint/tests/build, migration validation, and `git diff --check`
- [X] T027 Update implementation evidence and residual risks in `docs/specs/whatsapp/cadastro-templates-meta/quickstart.md`

## Dependencies

- Phase 2 depends on Phase 1.
- US1 depends on Phase 2.
- US2 depends on US1 because webhook reconciliation updates the catalog created there.
- US3 depends on the catalog from US1 and synchronization from US2.
- Polish depends on all user stories.

## Parallel opportunities

- T002 can proceed independently from T001.
- T008 can be written alongside T006 after domain APIs stabilize.
- T013 can be split by Meta adapter and worker test files.
- T024 can proceed after the frontend contract from T021 stabilizes.

## Implementation strategy

Deliver the durable backend path for US1 first, then add WABA-routed lifecycle convergence for US2, and finish with the TenantOwner catalog experience in US3. Each phase must retain tenant isolation and preserve the existing remote send revalidation.
