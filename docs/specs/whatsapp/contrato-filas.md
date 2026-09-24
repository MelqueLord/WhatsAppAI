# Queue API Contract Extension

`GET /api/service-queues` returns `transferNotice` for every queue.

`POST /api/service-queues` and `PUT /api/service-queues/{id}` accept optional `transferNotice`.

The value is either null or a customer-facing string no longer than 160 characters. The caller must have the existing tenant queue-management permission; other tenant behavior is unchanged.
