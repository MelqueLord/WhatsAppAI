# ADR-0008: PostgreSQL como banco único

**Status:** Accepted - 2026-08-26

## Contexto

O suporte simultâneo a múltiplos provedores criou divergência entre código, migrations, CI e produção. O ambiente gerenciado atual usa Supabase e a produção própria será executada na Hostinger com Docker.

## Decisão

PostgreSQL via Npgsql é o único banco da aplicação. Supabase fornece PostgreSQL gerenciado; a produção própria usa a imagem oficial PostgreSQL em Docker Compose. A mesma cadeia de migrations, testes e backup deve funcionar nos dois destinos. A conexão vem exclusivamente de configuração externa ou cofre.

## Consequências

- PostgreSQL é o único provider usado no runtime, CI e deploy.
- Testes de integração usam PostgreSQL real.
- Banco e porta PostgreSQL não são expostos publicamente em produção.
- Backups usam `pg_dump` e restaurações usam `pg_restore`.
- ADR-0006 fica superseded para decisões de banco e hosting; segredos continuam externos.
- ADR-0007 fica superseded porque PostgreSQL é obrigatório.
