# Pesquisa: multi-tenancy

Isolamento por tenant é requisito crítico e não uma otimização. O projeto usa `TenantId`, contexto corrente, filtros de consulta, autorização e testes negativos para impedir mistura de dados e eventos.

Fonte: [constituição](../../.specify/memory/constitution.md) e [modelo de ameaças](../regras/seguranca.md).
