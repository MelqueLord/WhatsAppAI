# Regras: backend

- Usar .NET 10, ASP.NET Core, EF Core e Npgsql conforme a arquitetura aprovada.
- Manter endpoints finos e regras de negócio em Domain/Application.
- Validar entrada, concorrência otimista e contexto de tenant antes de executar operação.
- Para persistência, criar migration reversível e testar banco real quando a mudança afetar integração.

Fonte: [plano técnico](../design/plano-plataforma.md) e [estratégia de testes](testes.md).
