# Design: processamento de mensagens

Webhook autenticado grava Inbox de forma idempotente. Worker normaliza contato, conversa e mensagem; a decisão automática revalida estado e cria mensagem e Outbox transacionalmente. O worker de saída entrega com retry e idempotência por operação externa.

Fonte: [arquitetura canônica](arquitetura-geral.md) e [ADR Inbox/Outbox](../decisoes/0005-postgres-inbox-outbox.md).
