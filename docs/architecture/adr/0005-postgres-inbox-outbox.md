# ADR-0005: Inbox/Outbox duráveis no PostgreSQL

**Status:** Aceito — 2026-08-03

## Decisão

Usar tabelas Inbox/Outbox com workers, locks com expiração, retry e dead-letter lógico. Não introduzir RabbitMQ ou Redis no MVP.

## Consequências

Persistência e intenção de envio participam da mesma transação. É preciso monitorar idade/profundidade das filas e evitar polling agressivo. Broker será reavaliado diante de limite medido.
