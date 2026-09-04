# Implementation Plan: Queue Transfer Notices

**Branch**: `master` | **Date**: 2026-09-04 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-queue-transfer-notices/spec.md`

**Note**: This template is filled in by the `$speckit-plan` command; its definition describes the execution workflow.

## Summary

Allow TenantOwners to configure an optional customer notice per service queue. When a queue transfer creates an outbound notification, the queue notice takes precedence over the existing tenant-wide transfer message; queues with no notice preserve the current fallback behavior.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C#/.NET 10 and TypeScript/React 19

**Primary Dependencies**: ASP.NET Core, EF Core, PostgreSQL, React Query

**Storage**: PostgreSQL `service_queues`

**Testing**: xUnit unit/integration tests and frontend component tests

**Target Platform**: Web application and Ubuntu production host

**Project Type**: Modular monolith web application

**Performance Goals**: Transfer notice is persisted with the existing handoff transaction and adds no additional external call.

**Constraints**: Tenant isolation, durable outbox, one notification per transfer, and 160-character customer-message limit.

**Scale/Scope**: Existing queues and new queues for all tenants; no new service or external dependency.

## Constitution Check

*GATE: Pass.* The feature reuses the modular monolith, tenant-scoped queue entity and durable Outbox. It adds no external service, keeps secrets out of scope, and provides testable tenant isolation.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file ($speckit-plan command output)
├── research.md          # Phase 0 output ($speckit-plan command)
├── data-model.md        # Phase 1 output ($speckit-plan command)
├── quickstart.md        # Phase 1 output ($speckit-plan command)
├── contracts/           # Phase 1 output ($speckit-plan command)
└── tasks.md             # Phase 2 output ($speckit-tasks command - NOT created by $speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
src/
├── WhatsAppAI.Domain/Messaging/ServiceLine.cs
├── WhatsAppAI.Infrastructure/Persistence/
├── WhatsAppAI.Infrastructure/Workers/AiOrchestrationWorker.cs
└── WhatsAppAI.WebApi/Queues/ServiceQueueEndpoints.cs
apps/web/src/features/queues/QueuesPage.tsx
tests/WhatsAppAI.UnitTests/
tests/WhatsAppAI.IntegrationTests/
```

**Structure Decision**: Extend the existing tenant queue entity, endpoint, queue settings screen and handoff worker.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
