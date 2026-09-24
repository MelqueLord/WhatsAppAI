# ADR-0007: Supabase PostgreSQL opcional

**Status:** Superseded by ADR-0008 — 2026-08-26

## Contexto

Uma implantação gerenciada no Supabase foi solicitada para os ambientes gerenciados.

## Decisão

Adicionar PostgreSQL via Npgsql como provedor explícito `SUPABASE`. A conexão vem somente de ambiente/cofre. Para o primeiro banco vazio, a inicialização controlada usa o modelo EF atual; migrations PostgreSQL próprias devem substituir esse bootstrap antes da primeira evolução de schema.

## Consequências

- PostgreSQL é o único banco suportado.
- Configurações específicas do provedor são mantidas no adaptador PostgreSQL.
- Segredos nunca ficam em scripts ou `appsettings` versionados.
- Alterações futuras de schema no Supabase exigem baseline/migrations PostgreSQL e teste de isolamento.
