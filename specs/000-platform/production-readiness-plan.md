# Plano de correção para produção

**Status:** NO-GO — correções de código principais validadas; gates operacionais e de release ainda pendentes
**Base da auditoria:** 2026-08-30
**Objetivo:** levar a plataforma a um deploy reproduzível e seguro em PostgreSQL, sem alterar o escopo do MVP.

## Estado atual

- Build .NET `Release`: aprovado em 2026-08-30, com 0 warnings e 0 errors.
- Testes unitários: 340 aprovados; testes de arquitetura: 7 aprovados.
- Frontend: lint, 24 testes e build aprovados. O build ainda emite avisos de chunk grande e de anotação `PURE` de dependência; devem ser avaliados antes de exigir “zero warning” no pipeline.
- `docker compose --profile production config`: aprovado com variáveis de teste preenchidas; isso não substitui validação com secrets e domínio reais.
- Migrations: `has-pending-model-changes` aprovado com conexão PostgreSQL de teste; bundle Docker gerado sem warnings de restore, aplicado em banco vazio e repetido com sucesso (12 migrations e segunda execução sem alterações).
- Startup de produção: API e worker iniciaram contra a base migrada com `Persistence__ApplyMigrationsOnStartup=false`; liveness/readiness da API retornaram HTTP 200. O Compose agora repassa também as credenciais obrigatórias de bootstrap do PlatformAdmin.
- Integração: após a correção do fixture de HTTP e da configuração PFX temporária do host de testes, a suíte completa passou: 67/67, 0 falhas e 0 ignorados, em 1m40s.
- Ainda não há evidência de `nginx -t`, HTTPS, SignalR por domínio, smoke test das integrações reais, backup/restore ou aprovação formal de Go/No-Go. O smoke local com PFX temporário eliminou os avisos de Data Protection; as duas consultas do worker e o binding HTTP explícito foram corrigidos no código/configuração e precisam de novo smoke para confirmação operacional.
- A tarefa `T013` da frente LGPD permanece aberta até fechar esses gates em `specs/002-lgpd-production-readiness/tasks.md`.

## Plano local de correção — 2026-08-30

Executar os pacotes abaixo nesta ordem. Cada pacote só pode ser encerrado com a evidência indicada; nenhum item deve ser marcado como concluído apenas por inspeção documental.

| Ordem | Prioridade | Pacote | Ação local | Critério de conclusão | Evidência |
|---|---|---|---|---|---|
| 1 | P0 | Suíte integrada determinística | Investigar o teste que impede a conclusão da suíte; executar unitários, arquitetura e integração em processos separados, com timeout e relatório TRX/JUnit | Todas as suítes terminam com exit code 0, sem teste crítico ignorado ou processo pendente | Logs do comando e relatório de testes |
| 2 | P0 | Migrations e startup | Aplicar migrations em PostgreSQL vazio e em cópia da versão anterior; validar `Up`/rollback; decidir se o `MigrateAsync` do WebApi permanece ou se somente o serviço `migrate` aplica schema | Banco novo e banco atualizado chegam ao mesmo snapshot; estratégia de concorrência documentada | Saída do migration bundle, diff do schema e decisão registrada |
| 3 | P0 | Deploy local equivalente | Preencher `.env` de teste, executar `docker compose --profile production config`, subir stack, validar health, frontend, API, WebSocket SignalR e `nginx -t` | Stack inicia reproduzivelmente e não expõe API/PostgreSQL diretamente | Logs do Compose, `nginx -t` e smoke HTTPS |
| 4 | P1 | Segurança e privacidade | Rodar scanner de segredos; revisar logs de webhook/IA/exportação; executar isolamento entre dois tenants e fluxo LGPD completo | Nenhum segredo/PII indevido em logs e zero acesso cruzado | Relatório do scanner, testes de isolamento e evidência LGPD |
| 5 | P1 | Integrações reais | Executar o workflow `Staging smoke` com Cloud API, QR, provedor de IA e SignalR configurados | Login, recebimento, envio, status, handoff e conexão real passam sem segredo no output | URL do workflow e logs sanitizados |
| 6 | P1 | Recuperação operacional | Criar backup, restaurar em staging isolado e medir RPO/RTO; testar rollback de imagem | RPO ≤ 24h, RTO ≤ 4h e rollback executável por pessoa designada | Backup, restore, timestamps e checklist assinado |
| 7 | P2 | Observabilidade e capacidade | Configurar OTLP, alertas, monitoramento de readiness e teste de carga somente leitura | Alertas chegam ao canal definido e limites de capacidade são conhecidos | Dashboard, alerta de teste e relatório de carga |
| 8 | P0 | Aprovação final | Revisar diff/tag, checklist, RIPD, responsáveis e plano de rollback | Todos os gates do contrato têm evidência e aprovação explícita | Registro de Go/No-Go e tag candidata |

### Critério de decisão

O resultado permanece **NO-GO** enquanto qualquer pacote P0/P1 estiver aberto, a integração completa não terminar com sucesso, houver teste crítico ignorado, ou backup/restore e HTTPS não tiverem sido ensaiados. Exceção somente mediante ADR com risco, responsável, prazo e aprovação, conforme a constituição.

### Progresso do plano

- **Pacote 1 — Suíte integrada determinística:** concluído em 2026-08-30. O fixture de HTTP passou a desabilitar hosted workers; o processamento dos workers continua coberto separadamente, enquanto os testes de endpoint não geram retries artificiais.
- **Pacote 2 — Migrations e startup:** concluído na validação local. O histórico EF foi fixado explicitamente em `public` para evitar a mudança de resolução causada pelo `search_path`; o bundle foi executado duas vezes na mesma base, e API/worker passaram a não aplicar migrations em Production. Rollback de banco, restore e validação em cópia de versão anterior continuam no pacote operacional.
- **Pacote 3 — Deploy local equivalente:** em andamento. Persistência e criptografia das chaves de Data Protection foram concluídas e validadas com reinício da API/worker; as consultas em lote do worker foram ordenadas e o binding foi alinhado a `ASPNETCORE_HTTP_PORTS`; faltam a validação do frontend atrás do proxy, Nginx, HTTPS e SignalR por domínio.
- **Próximo incremento:** validar o proxy completo e corrigir os avisos operacionais relevantes antes do staging.

## Progresso aplicado (2026-08-21)

- **PRD-001:** implementado. Cookie de sessão e antiforgery com `Secure` em produção e validação antiforgery para login + mutações autenticadas.
- **PRD-002:** implementado. `cookies.txt` removido do versionamento e rotação de nome de cookie para invalidar sessões anteriores.
- **PRD-003:** implementado em configuração. Variáveis obrigatórias unificadas entre `compose.yaml` e `deploy/.env.production.example`, sem defaults inseguros para segredos.
- **PRD-004:** implementado em configuração. Nginx movido para template (`deploy/nginx/default.conf.template`), `limit_req_zone` no contexto correto e dependência explícita de frontend no profile de produção.
- **PRD-005:** implementado em build/deploy. `EnsureCreated` removido de produção; migration bundle incorporado na imagem e serviço `migrate` adicionado ao `compose`.

## Pendências imediatas após implementação P0

- Provisionar o PFX definitivo e registrar a rotação das chaves de Data Protection no runbook.
- Validar `nginx -t` e smoke HTTPS/SignalR por domínio.
- Ensaiar migration em cópia de versão anterior, rollback e restauração.
- Executar scanner de segredos e smoke com integrações reais.
- Atualizar checklist e obter aprovação formal de segurança, LGPD e operação.

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
