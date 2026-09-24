# Contexto: multi-tenancy

`TenantId` é obrigatório para entidades de negócio. O tenant vem do contexto autenticado ou da linha validada do webhook, nunca de valor confiado ao cliente. Consultas, SignalR, jobs, caches, auditoria e acesso a mídia precisam preservar esse limite.

Fonte: [constituição](../../.specify/memory/constitution.md), [ameaças](../regras/seguranca.md) e [regras de multi-tenancy](../regras/multi-tenancy.md).
