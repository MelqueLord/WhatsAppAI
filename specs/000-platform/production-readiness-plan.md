# Plano de correção para produção

**Status:** Em andamento (P0 implementado em código; validações operacionais pendentes)  
**Base da auditoria:** 2026-08-21  
**Objetivo:** levar a plataforma a um deploy reproduzível e seguro em PostgreSQL, sem alterar o escopo do MVP.

## Estado atual

- Backend e frontend compilam em `Release`.
- Testes .NET: 226 aprovados, 3 falhando e 8 testes de webhook ignorados.
- Frontend: 1 teste falhando e 23 erros de lint.
- Existem alterações e migrations ainda não versionadas.
- Itens P0 de segurança/deploy foram implementados no código e configuração; falta validar em ambiente com Docker/TLS.

## Progresso aplicado (2026-08-21)

- **PRD-001:** implementado. Cookie de sessão e antiforgery com `Secure` em produção e validação antiforgery para login + mutações autenticadas.
- **PRD-002:** implementado. `cookies.txt` removido do versionamento e rotação de nome de cookie para invalidar sessões anteriores.
- **PRD-003:** implementado em configuração. Variáveis obrigatórias unificadas entre `compose.yaml` e `deploy/.env.production.example`, sem defaults inseguros para segredos.
- **PRD-004:** implementado em configuração. Nginx movido para template (`deploy/nginx/default.conf.template`), `limit_req_zone` no contexto correto e dependência explícita de frontend no profile de produção.
- **PRD-005:** implementado em build/deploy. `EnsureCreated` removido de produção; migration bundle incorporado na imagem e serviço `migrate` adicionado ao `compose`.

## Pendências imediatas após implementação P0

- Validar `docker compose --profile production config` em ambiente com Docker.
- Validar `nginx -t` e smoke HTTPS/SignalR por domínio.
- Fechar P1 aberto: 3 falhas de testes .NET, 23 erros de lint frontend e 1 teste frontend falhando.

Foi adicionado o workflow manual `Staging smoke` (`.github/workflows/staging-smoke.yml`)
com o script `apps/web/scripts/staging-smoke.mjs`. Ele gera a evidência reproduzível
dos testes reais de Cloud API, QR Code, provedor de IA e SignalR; a execução deve ser
feita no ambiente `staging` com suas credenciais armazenadas como secrets.

## Ordem de execução

| ID | Prioridade | Correção | Responsável sugerido | Aceite obrigatório |
|---|---|---|---|---|
| PRD-001 | P0 | Tornar cookies de sessão e antiforgery `Secure` em produção; validar antiforgery no login e em toda mutação autenticada. | Backend/Security | Testes negativos retornam 400/403 sem token; cookies produzidos via HTTPS têm `HttpOnly`, `Secure` e `SameSite=Lax`. |
| PRD-002 | P0 | Remover `cookies.txt` do versionamento, invalidar sessões existentes e verificar o histórico por segredos. | Security/DevOps | Scanner não encontra cookie, token, senha ou chave no repositório; sessões antigas deixam de ser aceitas. |
| PRD-003 | P0 | Unificar os nomes das variáveis entre `.env.production.example` e `compose.yaml`; proibir defaults inseguros em produção. | DevOps/Backend | `docker compose config` recebe todos os valores exigidos e falha cedo quando um segredo está ausente. |
| PRD-004 | P0 | Corrigir Nginx: template de domínio, contexto de rate limit, dependência do frontend, TLS e proxy de API/SignalR/webhook. | DevOps | `nginx -t` passa e o stack abre frontend, API e SignalR somente pelo domínio HTTPS. |
| PRD-005 | P0 | Substituir `EnsureCreated` em produção por migration bundle/job dedicado e reversível. | Backend/DBA | Banco PostgreSQL vazio sobe até a última migration; rollback ensaiado; API runtime não depende do SDK .NET. |
| PRD-006 | P1 | Consolidar e revisar as migrations não versionadas, incluindo filas, tags, linhas, operadores e IA. | Backend/DBA | Snapshot consistente; `Up`/`Down` testados em PostgreSQL; isolamento por tenant aprovado. |
| PRD-007 | P1 | Corrigir 23 erros de lint e o teste frontend de configuração da IA. | Frontend | `npm run lint`, `npm test` e `npm run build` passam sem erro. |
| PRD-008 | P1 | Habilitar os 8 testes de webhook com configuração controlada e adicionar cenários de assinatura, duplicidade e tenant. | Backend/QA | Nenhum teste crítico ignorado; webhook inválido é rejeitado e reentrega não duplica dados. |
| PRD-009 | P1 | Executar CI limpa com PostgreSQL e artefatos `Release`, incluindo migrations, isolamento e scanner de segredos. | QA/DevOps | Pipeline completo verde a partir de checkout limpo. |
| PRD-010 | P1 | Criar release versionada: revisar diff, separar mudanças por requisito e registrar riscos/rollback. | Tech Lead | Worktree limpo, commits rastreáveis e tag candidata imutável. |
| PRD-011 | P2 | Implantar staging equivalente à produção e executar smoke tests de PlatformAdmin, TenantOwner e Operator. | QA/Product | Login, convite, filas, tags, inbox, SignalR e permissões funcionam sem cruzamento de tenant. |
| PRD-012 | P2 | Validar Meta, WhatsApp QR e provedores de IA com credenciais de teste e logs sanitizados. | QA/Integrations | Entrada, saída, status, handoff, fila e tag funcionam; nenhum segredo aparece nos logs. |
| PRD-013 | P2 | Ativar domínio, TLS, health checks, monitoramento, alertas e coleta de logs. | DevOps | Readiness/liveness, alertas e dashboard operacional validados. |
| PRD-014 | P2 | Agendar backup e executar restauração completa em staging. | DevOps/DBA | Evidência de RPO ≤ 24h e RTO ≤ 4h conforme **NFR-005**. |
| PRD-015 | P2 | Realizar aprovação final de segurança, LGPD e negócio. | Security/Product | Checklist assinado; retenção, incidentes, acessos e responsável pelo rollback definidos. |

## Dependências e marcos

1. **Marco Segurança:** PRD-001 e PRD-002.
2. **Marco Deploy reproduzível:** PRD-003 a PRD-006.
3. **Marco Qualidade:** PRD-007 a PRD-010.
4. **Marco Staging:** PRD-011 a PRD-014.
5. **Go/No-Go:** PRD-015 e todos os gates do contrato aprovados.

PRD-011 não começa antes dos marcos Segurança, Deploy e Qualidade. Produção é proibida enquanto existir item P0/P1 aberto, teste crítico ignorado ou rollback não ensaiado.

## Riscos e rollback

- Migração incompatível: restaurar backup e voltar à imagem/tag anterior.
- Falha Meta/IA: manter conversas em modo humano e preservar Inbox/Outbox para reprocessamento.
- Falha de sessão após endurecimento: invalidar cookies e exigir novo login.
- Falha no proxy/TLS: retirar a nova release do balanceamento sem expor diretamente API ou banco.

## Evidências de conclusão

As evidências ficam anexadas à release: saída do CI, relatório de migrations, scanner de segredos, smoke test, teste de isolamento, `nginx -t`, backup/restore, health checks e aprovação Go/No-Go.
