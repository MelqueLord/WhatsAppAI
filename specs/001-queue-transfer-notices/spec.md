# Feature Specification: Queue Transfer Notices

**Feature Branch**: `001-queue-transfer-notices`

**Created**: 2026-09-04

**Status**: Ready for implementation

**Input**: User description: "agora implemente para cada fila que sera enviada antes de enviar uma mensagem de aviso seja enviada ao cliente; mensagem ao permanecer na fila e o cliente digitar algo deve ser ..."

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

---

### User Story 4 - Keep the customer informed while waiting (Priority: P1)

As a customer waiting in an automated queue, I want a clear status message when I send another message so I know where I am and how to request a different service.

**Why this priority**: Customers must not interpret silence as a connection failure, and queue keywords must remain available for changing the destination.

**Independent Test**: Put a conversation in an automatic queue, send a message without another queue keyword, and verify the queue-specific waiting message; then send a different queue keyword and verify the conversation changes queues.

**Acceptance Scenarios**:

1. **Given** a conversation remains in an automatic queue, **When** the customer sends another message without a different queue keyword, **Then** the customer receives `Aguarde, você está na fila {nome} para atendimento. Caso queira mudar seu atendimento, envie o tipo de atendimento que deseja.` and the conversation remains automated in the same queue.
2. **Given** a conversation remains in an automatic queue, **When** the customer sends a keyword belonging to another authorized queue, **Then** the conversation changes to that queue and the new queue transfer notice is sent.
3. **Given** a conversation is already in human mode, **When** the customer sends a message, **Then** the waiting rule does not send an automated response.

---

### User Story 5 - Continue AI service while queued (Priority: P1)

As a customer waiting in a queue, I want the configured AI service to continue applying the company's guidelines and knowledge so that queue assignment does not make the conversation unresponsive.

**Why this priority**: A queue is a routing state, not an automatic interruption. The company must be able to answer known requests immediately and escalate only what falls outside its service rules.

**Independent Test**: Put an automatic conversation in a queue, send a message covered by the company's guidelines or knowledge, and verify an AI response; then send an out-of-scope message and verify the configured human-transfer message and human mode.

**Acceptance Scenarios**:

1. **Given** an automatic conversation is in any queue, **When** the customer asks about a topic covered by the tenant guidelines or knowledge, **Then** the AI evaluates the full context and sends the applicable response or performs the applicable configured action.
2. **Given** an automatic conversation is in any queue, **When** the customer asks about a topic outside the configured service, **Then** the AI informs the customer that it will transfer the conversation to a human and the conversation changes to human mode.
3. **Given** a new automatic conversation, **When** the first message is processed by the AI, **Then** the configured business-specific welcome message is provided to the AI as the first-contact response guidance.

### Edge Cases

- A blank notice is treated as absent and falls back to the current tenant-wide or platform message.
- A notice longer than the existing customer message safety limit is rejected before it can be saved.
- Reprocessing the same inbound message does not create duplicate notices.
- Repeated messages while waiting use an idempotent customer-facing response per inbound message only when the AI returns no applicable action.
- A manual queue assignment does not itself assume the conversation; an explicit human mode action is required.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-QTN-001**: The system MUST allow a TenantOwner to view and save an optional transfer notice for each queue in the current company.
- **FR-QTN-002**: The system MUST use a queue's configured notice when that queue is selected for an automated transfer.
- **FR-QTN-003**: The system MUST fall back to the existing tenant-wide transfer notice, then to the existing platform default, when the selected queue has no notice.
- **FR-QTN-004**: The system MUST create at most one customer notice for each transfer event, including retries or duplicate webhook delivery.
- **FR-QTN-005**: The system MUST enforce the existing tenant and permission boundaries when reading or changing a queue notice.
- **FR-QTN-006**: The system MUST retain all existing transfer behavior for queues that have not yet been configured with a notice.
- **FR-QTN-007**: A conversation assigned automatically to any queue, including a human-service queue, MUST remain automated until a human explicitly takes over.
- **FR-QTN-008**: While a conversation remains in an automatic queue, each new inbound message without a different authorized queue keyword MUST be evaluated by the tenant AI using its guidelines, profile and relevant knowledge; the queue waiting message is sent only when the AI returns no applicable action.
- **FR-QTN-009**: An authorized keyword for a different queue MUST move the conversation to that queue and send its transfer notice before processing another automated reply.
- **FR-QTN-010**: Queue assignment MUST NOT bypass the tenant AI guidelines or knowledge; a covered request MUST receive the configured AI response or action even while waiting in a queue.
- **FR-QTN-011**: An out-of-scope AI decision MUST use the configured human-transfer message and switch the conversation to human mode.
- **FR-QTN-012**: A configured business-specific welcome message MUST be included as guidance for the AI on the first inbound message only.

### Key Entities

- **Queue transfer notice**: Optional customer-facing text attached to one queue in one company and used when that queue receives a conversation.
- **Queue transfer event**: A movement of a conversation to a queue that may create one outbound customer notification while automation remains active.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-QTN-001**: In automated tests, 100% of transfers to a queue with a notice create exactly one outbound message with that notice.
- **SC-QTN-002**: In automated tests, 100% of transfers to queues without a notice retain the previous fallback message behavior.
- **SC-QTN-003**: A TenantOwner can configure and save a queue notice in under one minute on desktop or mobile.
- **SC-QTN-004**: Tests confirm no queue notice from one company can be returned or used by another company.
- **SC-QTN-005**: 100% of follow-up messages in an automatic queue receive the current queue waiting message unless a different queue keyword is detected.
- **SC-QTN-006**: A customer keyword for another authorized queue changes the queue without invoking an unnecessary AI response.

## Assumptions

- The notice is sent as the existing transfer notification, immediately after the transfer is persisted and through the existing durable outbound delivery flow.
- Existing queues start with no notice and therefore require no migration action by the TenantOwner.
- The current customer-message character limit applies to a queue transfer notice.
- The standard waiting message is generated from the queue name and is not editable per tenant in this increment.
- A queue named for human service does not switch the conversation to human mode until an operator explicitly assumes it.
