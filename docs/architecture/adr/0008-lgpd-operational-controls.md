# ADR-0008: Controles operacionais de privacidade

**Status:** Accepted — 2026-08-27

## Contexto

A plataforma tratava conteúdo de contatos sem cadastro explícito de finalidade/base legal e não oferecia fluxo rastreável para direitos do titular. Documentação citava LGPD, mas RIPD, canal e encarregado permaneciam pendentes.

## Decisão

- Tenant é controlador dos dados de seus contatos; a plataforma é operadora. Para contas e segurança próprias, a plataforma é controladora.
- Base legal é registrada por finalidade. Consentimento só possui evidência quando for a hipótese escolhida.
- Direitos são executados por TenantOwner após validação externa da identidade do titular.
- Eliminação usa anonimização transacional de contato e conteúdo, preservando evidência mínima sem conteúdo pessoal.
- Identidade do controlador, canal e encarregado/dispensa vêm de configuração de ambiente e não bloqueiam o atendimento quando ausentes.
- Supabase/PostgreSQL é o ambiente de homologação e validação do candidato atual; segredos não são versionados.

## Consequências

- Novas entidades de privacidade carregam `TenantId` e filtro global.
- Mudanças de finalidade exigem revisão do RIPD e dos contratos com operadores/suboperadores.
- O deploy técnico pode prosseguir sem dados fictícios, mas a publicação da política real depende de configuração institucional fornecida pelo responsável do negócio.
