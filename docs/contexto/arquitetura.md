# Contexto: arquitetura

O sistema é um monólito modular: Domain e Application não dependem de SDKs externos; Infrastructure adapta PostgreSQL, Meta, Baileys e provedores de IA; WebApi compõe HTTP, autenticação e SignalR. PostgreSQL suporta transações, Inbox, Outbox e workers duráveis.

Fonte: [arquitetura canônica](../design/arquitetura-geral.md) e [ADRs](../decisoes/).
