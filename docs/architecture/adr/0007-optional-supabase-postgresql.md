# ADR-0007: Supabase PostgreSQL opcional

**Status:** Accepted — 2026-08-21

## Contexto

Uma implantação gerenciada no Supabase foi solicitada, enquanto MySQL 8.4 permanece suportado.

## Decisão

Adicionar PostgreSQL via Npgsql como provedor explícito `SUPABASE`. A conexão vem somente de ambiente/cofre. Para o primeiro banco vazio, a inicialização controlada usa o modelo EF atual; migrations PostgreSQL próprias devem substituir esse bootstrap antes da primeira evolução de schema.

## Consequências

- MySQL e SQLite continuam disponíveis.
- Configurações de tipos específicas de MySQL são neutralizadas no modelo PostgreSQL.
- Segredos nunca ficam em scripts ou `appsettings` versionados.
- Alterações futuras de schema no Supabase exigem baseline/migrations PostgreSQL e teste de isolamento.
