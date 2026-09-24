# Design: multi-tenancy

O contexto autenticado ou a linha validada resolve o tenant. Modelos persistidos, filtros, repositórios, eventos SignalR, auditoria e jobs propagam `TenantId`; nenhum endpoint escolhe tenant a partir de valor controlado pelo cliente.

Fonte: [modelo de dados](modelo-de-dados.md) e [modelo de ameaças](../regras/seguranca.md).
