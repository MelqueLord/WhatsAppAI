# Feature Specification: Queue Transfer Notices

**Feature Branch**: `001-queue-transfer-notices`

**Created**: 2026-09-04

**Status**: Ready for implementation

**Input**: User description: "agora implemente para cada fila que sera enviada antes de enviar uma mensagem de aviso seja enviada ao cliente"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Configure a notice for each queue (Priority: P1)

As a TenantOwner, I want to define a customer-facing notice for each active queue so the customer knows where their conversation is being directed.

**Why this priority**: A queue-specific notice makes automated routing understandable to the customer and avoids generic messages that do not explain the next step.

**Independent Test**: Configure a notice on one queue, transfer a conversation to it, and verify that the customer-facing outbound message uses that queue's notice.

**Acceptance Scenarios**:

1. **Given** an active queue, **When** the TenantOwner saves a notice, **Then** the notice remains associated only with that queue in the current company.
2. **Given** a queue with a configured notice, **When** a conversation is transferred to it automatically, **Then** exactly one outbound notice is queued for the customer as part of the transfer.

---

### User Story 2 - Keep existing queues working (Priority: P1)

As a TenantOwner, I want existing queues without a notice to continue working so enabling the feature does not interrupt customer service.

**Why this priority**: Existing companies must not need to edit every queue before routing can continue.

**Independent Test**: Transfer a conversation to a queue without a configured notice and verify that the existing generic transfer message is used.

**Acceptance Scenarios**:

1. **Given** a queue with no configured notice, **When** a conversation is transferred to it, **Then** the existing tenant-wide transfer message is used.
2. **Given** no tenant-wide transfer message, **When** a conversation is transferred to a queue without a notice, **Then** the existing platform default transfer message is used.

---

### User Story 3 - Preserve tenant isolation (Priority: P2)

As a PlatformAdmin, I want each company's queue notices isolated so one company's transfer wording cannot appear for another company.

**Why this priority**: Transfer notices are customer-facing business configuration and must follow the existing tenant boundary.

**Independent Test**: Create notices with distinct text for equivalent queues in two companies and verify each transfer uses only its own company's text.

**Acceptance Scenarios**:

1. **Given** two companies with their own queues, **When** one company updates its queue notice, **Then** the other company's configuration remains unchanged and inaccessible.

### Edge Cases

- A blank notice is treated as absent and falls back to the current tenant-wide or platform message.
- A notice longer than the existing customer message safety limit is rejected before it can be saved.
- Reprocessing the same inbound message does not create duplicate notices.
- A manual transfer follows the same queue-specific notice rule when it results in a customer notification.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-QTN-001**: The system MUST allow a TenantOwner to view and save an optional transfer notice for each queue in the current company.
- **FR-QTN-002**: The system MUST use a queue's configured notice when that queue is selected for an automated transfer.
- **FR-QTN-003**: The system MUST fall back to the existing tenant-wide transfer notice, then to the existing platform default, when the selected queue has no notice.
- **FR-QTN-004**: The system MUST create at most one customer notice for each transfer event, including retries or duplicate webhook delivery.
- **FR-QTN-005**: The system MUST enforce the existing tenant and permission boundaries when reading or changing a queue notice.
- **FR-QTN-006**: The system MUST retain all existing transfer behavior for queues that have not yet been configured with a notice.

### Key Entities

- **Queue transfer notice**: Optional customer-facing text attached to one queue in one company and used when that queue receives a conversation.
- **Queue transfer event**: A movement of a conversation to a human queue that may create one outbound customer notification.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-QTN-001**: In automated tests, 100% of transfers to a queue with a notice create exactly one outbound message with that notice.
- **SC-QTN-002**: In automated tests, 100% of transfers to queues without a notice retain the previous fallback message behavior.
- **SC-QTN-003**: A TenantOwner can configure and save a queue notice in under one minute on desktop or mobile.
- **SC-QTN-004**: Tests confirm no queue notice from one company can be returned or used by another company.

## Assumptions

- The notice is sent as the existing transfer notification, immediately after the transfer is persisted and through the existing durable outbound delivery flow.
- Existing queues start with no notice and therefore require no migration action by the TenantOwner.
- The current customer-message character limit applies to a queue transfer notice.
