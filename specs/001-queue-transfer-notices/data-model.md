# Data Model: Queue Transfer Notices

## Service queue

Add optional `TransferNotice` to a service queue.

- Belongs to the queue's existing tenant.
- Maximum 160 customer-facing characters.
- Blank text is stored as absent.
- Existing queues are initialized with no value.

## Transfer behavior

When a transfer selects a queue:

1. Use the queue notice when present.
2. Otherwise use the tenant-wide transfer message.
3. Otherwise use the existing platform default.

The existing transfer idempotency key remains the sole source for the outbound notice.
