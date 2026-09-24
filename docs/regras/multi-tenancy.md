# Regras: multi-tenancy

- Cada entidade comercial tem `TenantId`.
- O tenant nunca vem de body, query ou cliente como fonte confiável.
- Filtros, repositórios, jobs, SignalR, mídia, auditoria e cache devem restringir ao tenant atual.
- Todo incremento de dados ou acesso exige teste negativo entre dois tenants.

Fonte: [constituição](../../.specify/memory/constitution.md) e [modelo de ameaças](seguranca.md).
