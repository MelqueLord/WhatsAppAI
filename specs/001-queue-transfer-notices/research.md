# Research: Queue Transfer Notices

## Decision: Store the notice on the existing queue

**Rationale**: A notice belongs to one queue in one tenant. Keeping it on the existing queue preserves the tenant boundary and avoids a second configuration entity.

**Alternatives considered**: A standalone notice table would add no value because each queue needs only one optional text.

## Decision: Reuse the durable handoff and Outbox path

**Rationale**: The queue transfer and the customer notification must stay idempotent and commit together. The existing handoff helper already owns this transaction.

**Alternatives considered**: Sending from the API or frontend would risk duplicate or lost customer notices.

## Decision: Keep the current generic message as fallback

**Rationale**: Existing queues remain functional without configuration work, including in current production tenants.

**Alternatives considered**: Requiring a message for every queue would break existing routing after deployment.
