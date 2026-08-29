# Backlog de implementação

## Regras

- Execute em ordem de dependência; `[P]` indica paralelizável somente depois das dependências declaradas.
- Cada tarefa possui requisitos, caminhos/projetos esperados, dependências e validação de aceite.
- Cada tarefa termina com formatação, build/testes relevantes, revisão do diff e commit referenciando `T###` e os IDs rastreados.
- Não começar IA antes da entrada, inbox e envio humano estarem estáveis.
- Os ADRs vigentes são `docs/architecture/adr/0001-modular-monolith.md` a `0005-postgres-inbox-outbox.md`; não os duplicar.

## Fase 0 — Bootstrap e qualidade

- [X] **T001** Criar solution .NET 10, `global.json` e projetos do plano. **Refs:** NFR-007, R-001, ADR-0001. **Paths:** `WhatsAppAI.sln`, `global.json`, `src/*/*.csproj`, `tests/*/*.csproj`. **Depends:** nenhuma. **Aceite:** SDK fixado; restore e build da solution vazia passam sem warnings do projeto.
- [X] **T002** Criar React 19.2 + TypeScript + Vite. **Refs:** US-002, US-003, US-005, US-006, R-002. **Paths:** `apps/web/package.json`, `apps/web/src/`, lockfile. **Depends:** nenhuma. **Aceite:** dependências fixadas; lint, teste e build da SPA vazia passam.
- [X] **T003** Configurar MySQL 8.4 LTS no Docker Compose e EF Core/MySql.EntityFrameworkCore, sem criar migration. **Refs:** NFR-007, NFR-009, R-003, ADR-0005, ADR-0006. **Paths:** `compose.yaml`, `src/WhatsAppAI.Infrastructure/`, configuração local da WebApi. **Depends:** T001. **Aceite:** MySQL sobe, readiness passa e DbContext conecta sem schema de negócio/migration antecipada.
- [X] **T004** [P] Configurar formatadores, analyzers, nullable, warnings e testes de arquitetura. **Refs:** NFR-006, NFR-008, FR-004. **Paths:** `.editorconfig`, `Directory.Build.props`, `tests/WhatsAppAI.ArchitectureTests/`. **Depends:** T001. **Aceite:** formatação/análise passam e teste impede Domain/Application de depender de Infrastructure/SDKs externos.
- [X] **T005** [P] Configurar CI com restore, lint, build, testes e `git diff --check`. **Refs:** SC-005, NFR-006, NFR-008. **Paths:** `.github/workflows/ci.yml`, scripts de build na raiz e `apps/web/package.json`. **Depends:** T001, T002. **Aceite:** pipeline reproduzível executa todos os gates e falha com warning novo ou whitespace inválido.
- [X] **T006** Criar logging estruturado, correlation ID, Problem Details e health checks. **Refs:** US-007, FR-016, NFR-008. **Paths:** `src/WhatsAppAI.WebApi/`, `src/WhatsAppAI.Infrastructure/Observability/`, testes de integração. **Depends:** T001, T003, T004. **Aceite:** liveness/readiness e erro RFC 9457 funcionam; logs de teste não contêm segredo, prompt ou telefone completo.

**Marco:** solução vazia compila, sobe localmente e CI passa; nenhuma migration real foi criada.

## Fase 1 — Identidade, tenancy e administração

- [X] **T010** Modelar Tenant, User, TenantMembership e Invitation e criar a primeira migration real reversível. **Refs:** US-001, US-008, US-009, FR-002, FR-003, FR-025, FR-026, FR-028, BR-012, BR-013, BR-015, NFR-006. **Paths:** `src/WhatsAppAI.Domain/Identity/`, `src/WhatsAppAI.Infrastructure/Persistence/`, `src/WhatsAppAI.Infrastructure/Migrations/`. **Depends:** T003. **Aceite:** migration reversível cria membership com `user_id` único, estados/versão/security stamp e Invitation tenant-owned com `token_hash`, purpose, expiração, consumo, criador e revogação; nenhum token em claro é persistido.
- [X] **T011** Implementar ASP.NET Core Identity, cookie, antiforgery e invalidação por security stamp/estado da membership. **Refs:** FR-001, FR-002, FR-028, BR-015. **Paths:** `src/WhatsAppAI.Infrastructure/Identity/`, `src/WhatsAppAI.WebApi/Auth/`, `src/WhatsAppAI.WebApi/Program.cs`. **Depends:** T010. **Aceite:** produção usa cookie HttpOnly/Secure/SameSite=Lax; bootstrap entrega `X-CSRF-TOKEN`; cookie anterior à desativação é rejeitado e não volta a valer na reativação.
- [X] **T012** Implementar `ICurrentTenant`, papel, permissões e seleção administrativa explícita. **Refs:** FR-002, FR-026, FR-027, US-007, BR-012, NFR-006. **Paths:** `src/WhatsAppAI.Application/Abstractions/`, `src/WhatsAppAI.WebApi/Tenancy/`, testes unitários. **Depends:** T010, T011. **Aceite:** tenant/papel/permissões derivam da sessão e cada usuário possui no máximo uma membership; nenhum request usa `TenantId` arbitrário como bypass.
- [X] **T013** Aplicar filtros/guardas de tenant e testes negativos com dois tenants. **Refs:** FR-002, FR-026, NFR-006, SC-005. **Paths:** `src/WhatsAppAI.Infrastructure/Persistence/`, `tests/WhatsAppAI.IntegrationTests/Tenancy/`. **Depends:** T010, T012. **Aceite:** leitura/escrita cruzada falha, convite e membership carregam `tenant_id` e TenantOwner A não consulta/opera Operator de B.
- [X] **T014** Criar login/logout, `GET /auth/me`, bootstrap CSRF e shell autenticado da SPA. **Refs:** FR-001, FR-002, FR-027, US-002. **Paths:** `src/WhatsAppAI.WebApi/Auth/`, `apps/web/src/auth/`, `apps/web/src/app/`, cliente HTTP. **Depends:** T002, T011, T012. **Aceite:** login público obtém antiforgery; `/auth/me` retorna usuário, tenant, papel e permissões sanitizados; logout encerra sessão sem expor cookie ao JavaScript.
- [X] **T015** Implementar casos de uso/endpoints administrativos para criar tenant e convite do TenantOwner, suspender e reativar. **Refs:** US-001, US-008, FR-002, FR-003, FR-019, FR-025, BR-013, BR-014. **Paths:** `src/WhatsAppAI.Application/Tenants/`, `src/WhatsAppAI.Application/Identity/Invitations/`, `src/WhatsAppAI.WebApi/Admin/Tenants/`, testes unitários/integração. **Depends:** T010, T012, T013. **Aceite:** PlatformAdmin cria tenant/owner/invite atomicamente; resposta retorna o link uma única vez para entrega manual; suspensão preserva histórico e concorrência usa `If-Match`.
- [X] **T016** Criar interface administrativa de tenants com entrega manual do convite do TenantOwner. **Refs:** US-001, US-008, FR-003, FR-025, BR-014. **Paths:** `apps/web/src/features/admin/tenants/`, rotas protegidas e testes frontend. **Depends:** T014, T015. **Aceite:** PlatformAdmin cria/suspende/reativa; link do owner aparece somente na resposta de criação para cópia manual, não é retido no estado persistente e possui estados de erro/loading.
- [X] **T018** Implementar ativação pública de TenantOwner/Operator e tela de definição de senha. **Refs:** US-008, FR-025, BR-013, BR-014, SC-001. **Paths:** `src/WhatsAppAI.Application/Identity/Invitations/`, `src/WhatsAppAI.WebApi/Auth/Activate/`, `apps/web/src/features/auth/activate/`, testes unitários/integração/frontend. **Depends:** T011, T015. **Aceite:** token de uso único expira em 24 h; consumo e ativação de User/Membership são atômicos; token inválido/usado/expirado/revogado retorna erro sanitizado; nenhum e-mail é enviado.
- [X] **T019** Implementar aplicação, API e UI TenantOwner para listar, convidar, desativar, reativar e reenviar convite de Operators. **Refs:** US-009, FR-002, FR-025, FR-026, FR-028, BR-012, BR-013, BR-014, BR-015. **Paths:** `src/WhatsAppAI.Application/Identity/Operators/`, `src/WhatsAppAI.WebApi/Operators/`, `apps/web/src/features/operators/`, testes unitários/integração/frontend. **Depends:** T013, T014, T018. **Aceite:** apenas TenantOwner opera Operator do tenant corrente; reenvio revoga convite anterior e retorna novo link uma vez; desativação invalida sessões; reativação exige login; e-mail já associado a outro tenant é rejeitado sem enumeração.
- [X] **T017** Testar CSRF, convite, papéis, tenant e sessões negativamente em toda a Fase 1. **Refs:** US-008, US-009, FR-001, FR-002, FR-025, FR-026, FR-027, FR-028, BR-012, BR-013, BR-015, SC-001, SC-005. **Paths:** `tests/WhatsAppAI.IntegrationTests/Security/`, `tests/WhatsAppAI.IntegrationTests/Identity/`, testes frontend de auth/operators. **Depends:** T014, T015, T018, T019. **Aceite:** CSRF ausente/inválido falha; token de convite não reaproveita; Operator não administra memberships; acesso cruzado falha; cookie anterior à desativação é rejeitado; TenantOwner e Operator ativos recebem `/auth/me` correto.

**Bloqueio:** T001–T006 podem ser concluídas independentemente. A Fase 1 não está concluída e a Fase 2 não pode iniciar até T010–T019, inclusive ativação e gestão de Operators, passarem seus aceites.

**Marco:** PlatformAdmin cria tenant e convite; TenantOwner e pelo menos um Operator ativam, autenticam e recebem `/auth/me` correto; dois tenants não acessam usuários, convites ou dados um do outro.

## Fase 2 — Configuração e entrada da Meta

- [X] **T020** Definir `ISecretStore`, segredos globais do Meta App e implementações de desenvolvimento/teste. **Refs:** FR-004, FR-005, BR-008, BR-011. **Paths:** `src/WhatsAppAI.Application/Abstractions/ISecretStore.cs`, `src/WhatsAppAI.Infrastructure/Secrets/`, configuração da WebApi. **Depends:** T006, T010. **Aceite:** banco guarda somente `secret_ref`; `app_secret`, verify token e segredos de tenant nunca aparecem em resposta/log.
- [X] **T021** Implementar WhatsAppAccount e tela de configuração write-only. **Refs:** US-001, FR-004, FR-021, BR-001, BR-008, BR-011. **Paths:** Domain/Application/Infrastructure `Integrations/WhatsApp`, `apps/web/src/features/integrations/whatsapp/`. **Depends:** T013, T020. **Aceite:** WABA, `phone_number_id` e token pertencem ao tenant; apenas uma conta ativa; segredo nunca retorna ao browser.
- [X] **T022** Implementar `IWhatsAppClient` e teste de conexão Meta sanitizado. **Refs:** US-001, FR-021, BR-008. **Paths:** `src/WhatsAppAI.Application/Integrations/`, `src/WhatsAppAI.Infrastructure/Meta/`, testes de contrato. **Depends:** T020, T021. **Aceite:** sucesso/falha identifica etapa sem revelar credencial para o canal Cloud; conexões QR seguem o fluxo Baileys documentado em T168.
- [X] **T023** Implementar verificação GET e recepção POST do webhook com assinatura global. **Refs:** FR-005, BR-011, NFR-001. **Paths:** `src/WhatsAppAI.WebApi/Webhooks/`, `src/WhatsAppAI.Infrastructure/Meta/`. **Depends:** T020, T021. **Aceite:** assinatura valida bytes originais com app secret global antes de ler/resolver `phone_number_id`; limite/rate limit ativos.
- [X] **T024** Persistir WebhookEvent idempotente com envelope sanitizado e payload cifrado. **Refs:** FR-006, FR-007, FR-022, NFR-001, NFR-008. **Paths:** `src/WhatsAppAI.Domain/Messaging/`, `src/WhatsAppAI.Infrastructure/Persistence/`, migration e testes. **Depends:** T023. **Aceite:** ack não espera processamento; envelope e ciphertext são separados; reentrega não duplica.
- [X] **T025** Implementar worker com lock, retry, backoff e dead-letter lógico. **Refs:** BR-009, NFR-009, ADR-0005. **Paths:** `src/WhatsAppAI.Infrastructure/Workers/`, testes unitários/integração. **Depends:** T024. **Aceite:** falha transitória reprograma; limite envia a `Dead` e produz métrica/alerta sem loop.
- [X] **T026** Normalizar contatos, conversas, mensagens e status. **Refs:** US-002, FR-008, BR-002, BR-005. **Paths:** Domain/Application `Messaging/`, persistência e testes. **Depends:** T024, T025. **Aceite:** evento reconhecido cria/atualiza somente dados do tenant resolvido e renova janela apenas com inbound do cliente.
- [X] **T027** Testar fixtures Meta, assinatura inválida, ordem, payload grande e reentrega em massa. **Refs:** FR-005, FR-006, FR-007, SC-002, SC-003. **Paths:** `tests/WhatsAppAI.IntegrationTests/Contracts/Meta/`, `tests/WhatsAppAI.IntegrationTests/Webhooks/`. **Depends:** T023, T024, T026. **Aceite:** amostra de pelo menos 1.000 eventos cumpre SC-002/003 e assinatura inválida nunca persiste evento.
- [X] **T028** Classificar, consultar e reprocessar eventos desconhecidos com auditoria. **Refs:** US-002, US-007, FR-019, FR-022, NFR-008. **Paths:** Application/WebApi `WebhookEvents/`, `apps/web/src/features/admin/webhooks/`, testes de integração. **Depends:** T024, T025, T012. **Aceite:** desconhecido fica `Unknown`/quarentenado, consulta expõe somente envelope sanitizado e reprocessamento autorizado/auditado usa payload decifrado internamente.

**Marco:** mensagem real da Meta é persistida uma única vez; evento desconhecido é recuperável sem expor payload.

## Fase 3 — Inbox em tempo real

- [X] **T030** Implementar consultas paginadas por cursor de conversas e mensagens conforme OpenAPI. **Refs:** US-002, FR-008, FR-024. **Paths:** `src/WhatsAppAI.Application/Conversations/Queries/`, `src/WhatsAppAI.WebApi/Conversations/`, testes de integração. **Depends:** T026. **Aceite:** cursores estáveis, limite máximo 100 e nenhuma mensagem de outro tenant.
- [X] **T031** Implementar hub SignalR autenticado com grupo por tenant. **Refs:** FR-009, NFR-006. **Paths:** `src/WhatsAppAI.WebApi/Hubs/`, `src/WhatsAppAI.Application/Messaging/`. **Depends:** T012, T026. **Aceite:** servidor escolhe grupo pelo contexto e rejeita inscrição arbitrária.
- [X] **T032** Criar lista de conversas e painel paginado de mensagens. **Refs:** US-002, FR-024. **Paths:** `apps/web/src/features/inbox/`, testes frontend. **Depends:** T014, T030. **Aceite:** estados vazio/erro/loading, cursor e troca de conversa funcionam sem perder cache consistente.
- [X] **T033** Atualizar inbox em tempo real e reconciliar cache da API. **Refs:** US-002, FR-009, NFR-002. **Paths:** `apps/web/src/features/inbox/`, cliente SignalR e testes. **Depends:** T031, T032. **Aceite:** mensagem persistida aparece no tenant correto em p95 <3 s no teste definido.
- [X] **T034** Implementar metadados e download/proxy autenticado de mídia e exibição com fallback. **Refs:** US-002, FR-023, NFR-006, NFR-008. **Paths:** Application/WebApi `Media/`, `src/WhatsAppAI.Infrastructure/Meta/`, `apps/web/src/features/inbox/media/`. **Depends:** T022, T026, T030, T032. **Aceite:** imagem/documento/áudio suportado baixa pelo endpoint tenant-scoped; token/URL Meta não aparece em contrato, browser ou log.
- [X] **T035** Testar SignalR e mídia contra acesso cruzado. **Refs:** FR-009, FR-023, NFR-006, SC-005. **Paths:** `tests/WhatsAppAI.IntegrationTests/Realtime/`, `tests/WhatsAppAI.IntegrationTests/Media/`. **Depends:** T031, T034. **Aceite:** usuário A não recebe evento nem baixa mídia de B.

**Marco:** inbox operacional recebe mensagens, pagina histórico e acessa mídia segura em tempo real.

## Fase 4 — Resposta humana e saída durável

- [X] **T040** Implementar mudança de modo com `If-Match`, versão e HandoffEvent. **Refs:** US-003, FR-011, BR-003, BR-004. **Paths:** Domain/Application/WebApi `Conversations/`, testes unitários. **Depends:** T030. **Aceite:** versão obsoleta retorna conflito e assumir grava `Human`/auditoria atomicamente.
- [X] **T041** Implementar regra da janela de 24 horas com `IClock`. **Refs:** FR-012, BR-005, BR-006. **Paths:** Domain/Application `Messaging/`, testes unitários. **Depends:** T026. **Aceite:** bordas antes/no/depois de 24 h passam; somente inbound do cliente renova janela.
- [X] **T042** Criar Message + OutboxMessage na mesma transação e endpoint idempotente. **Refs:** US-003, FR-010, FR-012, NFR-009, ADR-0005. **Paths:** Application/WebApi `Messages/`, Infrastructure persistence/migration. **Depends:** T040, T041. **Aceite:** `Idempotency-Key` repetida retorna o mesmo resultado e nenhuma intenção de envio se perde.
- [X] **T043** Implementar dispatcher Meta, retry seletivo e estados de entrega. **Refs:** US-003, FR-010, BR-009, NFR-009. **Paths:** `src/WhatsAppAI.Infrastructure/Meta/`, workers Outbox e testes. **Depends:** T022, T042. **Aceite:** estados avançam sem regressão/duplicação e falha definitiva gera alerta sanitizado.
- [X] **T044** Integrar compositor com modo e bloqueio/explicação de janela fechada. **Refs:** US-003, FR-011, FR-012, BR-003, BR-006. **Paths:** `apps/web/src/features/inbox/composer/`, testes frontend. **Depends:** T032, T040, T042. **Aceite:** operador assume antes de enviar; texto/template fora da janela não é submetido.
- [X] **T045** Testar concorrência, duplicação, timeout e falha definitiva. **Refs:** FR-010, FR-011, NFR-009, SC-003, SC-004. **Paths:** `tests/WhatsAppAI.IntegrationTests/Outbound/`, testes unitários. **Depends:** T040–T043. **Aceite:** corrida, timeout após envio e webhook tardio não duplicam nem violam modo.

**Marco:** Operator do piloto assume e responde uma conversa real com recuperação de falhas.

## Fase 5 — IA segura

- [X] **T050** Implementar AiProviderCredential e configuração write-only. **Refs:** US-001, FR-004, FR-021, BR-008. **Paths:** Domain/Application/Infrastructure `Integrations/OpenAI/`, `apps/web/src/features/integrations/ai/`. **Depends:** T020, T013. **Aceite:** chave passa pelo cofre, nunca retorna ao browser e modelo é configuração, não regra de domínio.
- [X] **T051** Definir `IAiProvider`, `AiDecision` e adaptador Responses API. **Refs:** US-004, FR-014, R-004. **Paths:** `src/WhatsAppAI.Application/Automation/`, `src/WhatsAppAI.Infrastructure/OpenAI/`, `tests/WhatsAppAI.IntegrationTests/Contracts/OpenAI/`. **Depends:** T050. **Aceite:** schema estruturado rejeita ação/texto inválido e SDK não vaza para Domain/Application.
- [X] **T052** Criar montador de contexto com política, histórico limitado e orçamento. **Refs:** US-004, FR-013, NFR-008, R-005. **Paths:** `src/WhatsAppAI.Application/Automation/Context/`, testes unitários. **Depends:** T030, T051. **Aceite:** usa somente tenant/conversa/itens ativos necessários e respeita limites determinísticos.
- [X] **T053** Implementar orquestração após inbound em modo Automatic. **Refs:** US-004, FR-013, FR-014, BR-003. **Paths:** `src/WhatsAppAI.Application/Automation/`, worker e testes. **Depends:** T025, T042, T051, T052. **Aceite:** Human/Paused nunca chama IA; falha não perde inbound nem cria loop.
- [X] **T054** Revalidar modo, janela e versão antes do Outbox. **Refs:** FR-015, BR-004, BR-010, SC-004. **Paths:** `src/WhatsAppAI.Application/Automation/`, testes concorrentes. **Depends:** T040, T041, T053. **Aceite:** qualquer versão/mode/window alterado descarta a decisão antes do envio.
- [X] **T055** Persistir AiInteraction e UsageLedger sem prompt completo. **Refs:** FR-016, FR-018, NFR-008. **Paths:** Domain/Infrastructure `Automation/` e `Usage/`, migration e testes. **Depends:** T051, T053. **Aceite:** somente modelo, tokens, latência, decisão e códigos sanitizados são persistidos; prompt/resposta bruta/PII não existem no schema.
- [X] **T056** Aplicar `docs/ai/behavior-policy.md` e circuit breaker local. **Refs:** US-004, FR-014, FR-015, BR-009. **Paths:** `docs/ai/behavior-policy.md`, `src/WhatsAppAI.Application/Automation/Policy/`, testes unitários. **Depends:** T053, T054. **Aceite:** todos os motivos normativos de handoff têm caso testado e backend conserva decisão final.
- [X] **T057** Executar gate de escolha/promoção do modelo. **Refs:** US-004, FR-014, NFR-003, SC-004, R-004. **Paths:** `tests/WhatsAppAI.IntegrationTests/AiEvaluations/`, `docs/runbooks/ai-model-evaluation.md`, configuração de deploy. **Depends:** T051, T052, T056. **Aceite:** candidato aprovado registra qualidade, handoff, segurança, custo, p95, aprovador e rollback; nenhum modelo muda sem o gate.
- [X] **T058** Testar corrida “IA gerando enquanto operador assume”. **Refs:** FR-011, FR-015, BR-004, BR-010, SC-004. **Paths:** `tests/WhatsAppAI.IntegrationTests/Automation/`. **Depends:** T054. **Aceite:** operador vence em 100% das execuções e nenhuma resposta automática posterior é enfileirada.
- [X] **T059** Implementar e testar explicitamente a conexão OpenAI sanitizada. **Refs:** US-001, FR-004, FR-021. **Paths:** Application/WebApi `Integrations/OpenAI/TestConnection`, Infrastructure OpenAI e testes de contrato. **Depends:** T050, T051. **Aceite:** endpoint informa etapa/código sem revelar chave, request ou resposta bruta.

**Marco:** IA responde texto seguro, atende NFR-003 e cede controle deterministicamente.

## Fase 6 — Conhecimento e configuração do negócio

- [X] **T060** Implementar KnowledgeItem com `If-Match`, criação, edição, ativação, desativação e auditoria. **Refs:** US-005, FR-017, FR-019. **Paths:** Domain/Application/WebApi `Knowledge/`, persistence/migration e testes. **Depends:** T013, T017. **Aceite:** versão obsoleta conflita, desativação preserva registro e nenhum endpoint de delete físico existe.
- [X] **T061** Criar tela de conhecimento com ativação, prioridade e limites. **Refs:** US-005, FR-017. **Paths:** `apps/web/src/features/knowledge/`, testes frontend. **Depends:** T060. **Aceite:** UI envia `If-Match`, trata 409 e oferece desativar/reativar sem excluir.
- [X] **T062** Integrar seleção determinística de itens ativos ao contexto. **Refs:** US-004, US-005, FR-013, FR-017, R-005. **Paths:** `src/WhatsAppAI.Application/Knowledge/`, `Automation/Context/`. **Depends:** T052, T060. **Aceite:** apenas itens ativos do tenant entram na próxima interação dentro do orçamento.
- [X] **T063** Testar isolamento, concorrência, limite e atualização de conhecimento. **Refs:** US-005, FR-013, FR-017, NFR-006, SC-005. **Paths:** `tests/WhatsAppAI.IntegrationTests/Knowledge/`, avaliações IA. **Depends:** T060–T062. **Aceite:** dois tenants não cruzam itens; limite e versão funcionam; alteração aparece somente na interação seguinte.

**Marco:** proprietário ajusta respostas sem deploy e sem exclusão física de conhecimento.

## Fase 7 — Uso, auditoria e retenção

- [X] **T070** Completar UsageLedger com preços versionados opcionais e custo menor inteiro. **Refs:** US-006, FR-018, BR-007, NFR-006. **Paths:** Domain/Application/Infrastructure `Usage/`, migration e testes. **Depends:** T055. **Aceite:** custo usa inteiro + moeda ISO; unicidade é `(tenant_id, provider, metric, source_id)`.
- [X] **T071** Criar painel de unidades/estimativas e disclaimer. **Refs:** US-006, FR-018, BR-007. **Paths:** `apps/web/src/features/usage/`, API Usage e testes. **Depends:** T070. **Aceite:** separa provedor/período, formata unidade menor sem perda e informa que não é fatura.
- [X] **T072** Implementar AuditLog imutável para ações sensíveis. **Refs:** US-007, FR-019, NFR-008. **Paths:** Domain/Infrastructure `Audit/`, migration/permissões e testes. **Depends:** T010, T020. **Aceite:** identidade da aplicação não possui UPDATE/DELETE; tentativa falha e correção cria novo evento.
- [X] **T073** Implementar jobs de retenção e exclusão operacional. **Refs:** FR-020, NFR-008. **Paths:** Application/Infrastructure `Retention/`, migrations e runbook. **Depends:** T072 e decisão de retenção aprovada. **Aceite:** operação é tenant-scoped, auditada, recuperável conforme política e preserva evidência obrigatória.
- [X] **T074** Testar ausência de segredos, prompt completo e PII em persistência, logs, erros e exportações. **Refs:** FR-004, FR-016, NFR-008, SC-005. **Paths:** `tests/WhatsAppAI.IntegrationTests/Security/`, scanner/configuração CI. **Depends:** T055, T070–T073. **Aceite:** fixtures sentinela não aparecem em nenhuma superfície proibida.
- [X] **T075** Testar colisão de UsageLedger entre tenants. **Refs:** FR-018, BR-007, NFR-006, SC-005. **Paths:** `tests/WhatsAppAI.IntegrationTests/Usage/`. **Depends:** T070. **Aceite:** mesmo `(provider, metric, source_id)` é aceito em tenants distintos e rejeitado quando duplicado no mesmo tenant.

## Fase 8 — Produção e piloto

- [X] **T080** Escolher hospedagem/cofre gerenciado, registrar a nova decisão e implementar `ISecretStore` de produção. **Refs:** FR-004, FR-005, BR-008, BR-011. **Paths:** ADR-0006, `deploy/.env.production.example`, `src/WhatsAppAI.Infrastructure/Secrets/`.
- [X] **T081** Configurar TLS, CORS, headers, rate limits, backup e restore ensaiado. **Refs:** FR-001, FR-005, NFR-005, NFR-008. **Paths:** `deploy/nginx/default.conf.template`, `deploy/backup.sh`, `deploy/restore.sh`, `docs/runbooks/disaster-recovery.md`.
- [X] **T082** Exportar métricas/traces, dashboards e alertas e calcular o SLI mensal. **Refs:** US-007, NFR-004, BR-009, SC-006. **Paths:** `docs/runbooks/observability.md`.
- [X] **T083** Executar testes de carga das metas de webhook, inbox, IA e capacidade. **Refs:** NFR-001, NFR-002, NFR-003, NFR-007. **Paths:** `docs/testing/load-test-plan.md`.
- [X] **T084** Atualizar threat model e executar checklist LGPD com revisão jurídica independente. **Refs:** FR-004, FR-016, FR-022, FR-023, NFR-006, NFR-008, SC-005. **Paths:** `docs/security/threat-model.md`, `docs/security/lgpd-checklist.md`.
- [X] **T085** Provisionar tenant piloto e medir os seis critérios. **Refs:** US-008, US-009, SC-001 a SC-006. **Paths:** `docs/pilot/runbook.md`.
- [X] **T086** Registrar incidentes, feedback e decisões necessárias antes de ampliar vendas. **Refs:** US-007, SC-006, NFR-004, NFR-005. **Paths:** `docs/pilot/incidents.md`.

## Backlog posterior, não autorizado no MVP

- Templates utilitários aprovados para janela fechada (**BR-006**, **R-009**).
- Múltiplos números/equipes, RAG vetorial, canais adicionais, billing interno e integrações periféricas (**BR-001**, **R-005**, **R-008**).
- Redis, broker ou microsserviços somente após critérios de `research.md` e novo ADR (**NFR-007**, ADR-0001, ADR-0005).

## Gaps e dívidas técnicas (2026-08-16)

| # | Gap | Impacto | Status |
|---|---|---|---|
| ~~G1~~ | ~~**Dockerfiles ausentes**~~ | ~~Deploy impossível~~ | Resolvido (T117-T118) |
| ~~G2~~ | ~~**CI usa PostgreSQL**~~ | ~~CI não reflete produção~~ | Resolvido (T119) |
| G3 | **16 integration tests falhando** | Qualidade comprometida | Pendente |
| ~~G4~~ | ~~**Debug code pendente**~~ | ~~Segurança/limpeza~~ | Resolvido (T120) |
| ~~G5~~ | ~~**BotConfiguration não integrado**~~ | ~~Modo Manual/SimpleAutoReply inoperante~~ | Resolvido (T121) |
| G6 | **Test coverage** (~187 testes: 185 unit + 2 frontend) | Regressões não detectadas | Melhorado (T125-T144, T158-T161, T165) |
| G7 | **Sem serviço de e-mail** | Onboarding manual | Backlog |
| G8 | **Sem template messages** | Comunicação pós-24h impossível | Backlog |

### Fase 8 — Produção e piloto: COMPLETA (T080-T086)

### Fase 9 — Sistema de Planos: COMPLETA (T090-T116)

### Tasks adicionais: COMPLETA (T117-T144)

## Feature: Sistema de Planos e Gestão de Empresas

**Spec:** `specs/000-platform/spec-planos.md`  
**Status:** Implementado (Fase 9 - T090-T116)
**Dependências:** Plataforma base (T001-T075) estável

### Resumo

Permitir que PlatformAdmin cadastre empresas com dois tipos de plano:
- **BOT:** Todos os recursos da plataforma, exceto IA para atendimento
- **IA+BOT:** Completo com IA para atendimento automatizado

### Tasks planejadas (fase 9)

- [X] **T090** Modelar SubscriptionPlan e alterar Tenant com plan_id. **Refs:** FR-P001, FR-P002, BR-P001.
- [X] **T091** Implementar endpoint de cadastro de empresa (Tenant + TenantOwner + Invitation). **Refs:** US-P001, FR-P006, FR-P007, FR-P008.
- [X] **T092** Criar interface de cadastro de empresa com seleção de plano. **Refs:** US-P001, FR-P009.
- [X] **T093** Implementar filtros de funcionalidades de IA por plano no frontend. **Refs:** US-P002, US-P005, FR-P010.
- [X] **T094** Implementar validação de plano nos endpoints de IA. **Refs:** FR-P011, BR-P002.
- [X] **T095** Adaptar dashboard para mostrar plano e funcionalidades. **Refs:** US-P005.
- [X] **T096** Testar isolamento entre planos (BOT não usa IA). **Refs:** BR-P002.
- [X] **T097** Filtrar modo AiPowered no BotConfiguration por plano. **Refs:** BR-P002, FR-P011.
- [X] **T098** Filtrar métricas OpenAI no UsagePage por plano. **Refs:** FR-P010.
- [X] **T099** Validar plano nos endpoints de ModelEvaluation. **Refs:** FR-P011, BR-P002.
- [X] **T100** Testes de isolamento completos entre planos. **Refs:** BR-P002, BR-P003.
- [X] **T101** Endpoint para alterar plano do tenant (FR-P005). **Refs:** FR-P005, BR-P004.
- [X] **T102** Frontend para alterar plano no admin. **Refs:** FR-P005.
- [X] **T103** AiOrchestrationWorker verifica plano antes de processar IA. **Refs:** BR-P002.
- [X] **T104** Atualizar tasks.md com Fase 9 completa.
- [X] **T105** Testes de upgrade/downgrade de plano. **Refs:** FR-P005, BR-P004.
- [X] **T106** Testes de endpoints de IA com diferentes planos. **Refs:** FR-P011, BR-P002.
- [X] **T107** Atualizar data-model.md com SubscriptionPlan. **Refs:** FR-P001, FR-P002.
- [X] **T108** Atualizar tasks.md com Fase 9 completa.
- [X] **T109** Atualizar spec-planos.md com status de implementação. **Refs:** FR-P001.
- [X] **T110** Testes unitários para SubscriptionPlan. **Refs:** FR-P001, FR-P002.
- [X] **T111** Atualizar spec.md com referência à Fase 9. **Refs:** spec-planos.md.
- [X] **T112** Testes unitários para Tenant.ChangePlan. **Refs:** FR-P005.
- [X] **T113** Corrigir tasks.md (T090-T096 já implementadas). **Refs:** tasks.md.
- [X] **T114** Testes de integração para endpoint de alterar plano. **Refs:** FR-P005, BR-P004.
- [X] **T115** Atualizar quickstart.md com informações sobre planos. **Refs:** spec-planos.md.
- [X] **T116** Atualizar tasks.md com Fase 9 completa. **Refs:** tasks.md.
- [X] **T117** Criar Dockerfile para WebApi. **Refs:** G1, T080.
- [X] **T118** Criar Dockerfile para Frontend + nginx.conf. **Refs:** G1, T080.
- [X] **T119** Corrigir CI para usar MySQL. **Refs:** G2, T005.
- [X] **T120** Verificar/remover debug code. **Refs:** G4.
- [X] **T121** Integrar BotConfiguration.Mode no AiOrchestrationWorker. **Refs:** G5, T053.
- [X] **T122** Implementar SimpleAutoReply mode (fallback/welcome). **Refs:** G5, BR-P002.
- [X] **T123** Atualizar gaps.md (G5 resolvido). **Refs:** G5.
- [X] **T124** Atualizar tasks.md. **Refs:** tasks.md.
- [X] **T125** Testes unitários para BotConfiguration. **Refs:** G6, T121.
- [X] **T126** Testes unitários para Conversation. **Refs:** G6, T040.
- [X] **T127** Atualizar G4 como resolvido. **Refs:** G4.
- [X] **T128** Atualizar tasks.md. **Refs:** tasks.md.
- [X] **T129** Testes unitários para Message. **Refs:** G6, T042.
- [X] **T130** Testes unitários para OutboxMessage. **Refs:** G6, T043.
- [X] **T131** Testes unitários para WebhookEvent. **Refs:** G6, T024.
- [X] **T132** Atualizar tasks.md. **Refs:** tasks.md.
- [X] **T133** Testes unitários para KnowledgeItem. **Refs:** G6, T060.
- [X] **T134** Testes unitários para AuditLog. **Refs:** G6, T072.
- [X] **T135** Testes unitários para UsageLedger. **Refs:** G6, T070.
- [X] **T136** Atualizar tasks.md. **Refs:** tasks.md.
- [X] **T137** Testes unitários para TenantMembership. **Refs:** G6, T019.
- [X] **T138** Testes unitários para Invitation. **Refs:** G6, T018.
- [X] **T139** Testes unitários para AiInteraction. **Refs:** G6, T055.
- [X] **T140** Atualizar tasks.md. **Refs:** tasks.md.
- [X] **T141** Testes unitários para HandoffEvent. **Refs:** G6, T040.
- [X] **T142** Testes unitários para Contact. **Refs:** G6, T026.
- [X] **T143** Testes unitários para User. **Refs:** G6, T011.
- [X] **T144** Atualizar tasks.md. **Refs:** tasks.md.
- [X] **T087** Atualizar gaps após Fase 8. **Refs:** tasks.md.
- [X] **T088** Criar deployment checklist. **Refs:** T080-T086.
- [X] **T089** Atualizar tasks.md. **Refs:** tasks.md.

### Nota: T090-T095 já implementados (Fase 9 - Sistema de Planos)

## Feature: Multi-provedor de IA e configurações separadas por plano

**Spec:** `specs/000-platform/spec-ai-multi-provider.md`
**Status:** Implementado (Fase 10 - T150-T165)
**Dependências:** Plataforma base (T001-T075), Sistema de Planos (T090-T116)

### Fase 10 — Multi-provedor de IA: COMPLETA (T150-T165)

- [X] **T150** Criar `IAiProviderResolver` e refatorar registro DI para suportar múltiplos provedores. **Refs:** FR-AI-001, FR-AI-002, BR-AI-001.
- [X] **T151** [P] Implementar `GeminiProvider : IAiProvider`. **Refs:** FR-AI-002, US-AI-001.
- [X] **T152** [P] Implementar `AnthropicProvider : IAiProvider`. **Refs:** FR-AI-002, US-AI-001.
- [X] **T153** [P] Implementar `XiaomiProvider : IAiProvider`. **Refs:** FR-AI-002, US-AI-001.
- [X] **T154** Registrar todos os provedores no DI e criar extensão `AddAiProviderServices`. **Refs:** FR-AI-002.
- [X] **T155** Atualizar `AiOrchestrationWorker` para usar `IAiProviderResolver` e registrar provedor no `UsageLedger`. **Refs:** FR-AI-001, BR-AI-001.
- [X] **T156** Atualizar endpoints de IA para aceitar `provider` e listar modelos por provedor. **Refs:** FR-AI-001, FR-AI-006, FR-AI-007.
- [X] **T157** Manter endpoints de `BotConfiguration` separados dos endpoints de IA e aplicar as permissões por pacote. **Refs:** FR-AI-003, FR-AI-005, US-AI-002.
- [X] **T158** [P] Testes unitários para `GeminiProvider` (6 testes). **Refs:** FR-AI-002, G6.
- [X] **T159** [P] Testes unitários para `AnthropicProvider` (6 testes). **Refs:** FR-AI-002, G6.
- [X] **T160** [P] Testes unitários para `XiaomiProvider` (6 testes). **Refs:** FR-AI-002, G6.
- [X] **T161** Testes unitários para `IAiProviderResolver` (5 testes). **Refs:** FR-AI-002, G6.
- [X] **T162** Testes de integração para multi-provedor (4 testes). **Refs:** FR-AI-002, BR-AI-001, BR-AI-002.
- [X] **T163** Reescrever `AiConfigPage` com seletor de provedor e seções próprias de IA. **Refs:** FR-AI-003, FR-AI-006, FR-AI-007, US-AI-002.
- [X] **T164** Manter `BotConfigPage` e as rotas distintas, com visibilidade condicionada ao pacote. **Refs:** FR-AI-003.
- [X] **T165** Testes frontend para telas separadas e permissões por pacote. **Refs:** FR-AI-003, G6.

### Incremento de capacidade de linhas por tenant

- [X] **T166** Adicionar limites de linhas por API oficial e QR Code no cadastro administrativo de tenants, com persistência, migration, validação e UI. **Refs:** FR-029, US-001. **Paths:** `src/WhatsAppAI.Domain/Identity/Tenant.cs`, `src/WhatsAppAI.Infrastructure/Persistence/Migrations/`, `src/WhatsAppAI.WebApi/Admin/AdminTenantEndpoints.cs`, `apps/web/src/features/admin/tenants/`. **Depends:** T015, T016. **Aceite:** valores inteiros não negativos são salvos e exibidos separadamente por canal; valores negativos são rejeitados; a regra de um número ativo por tenant permanece inalterada.
- [X] **T167** Implementar edição administrativa de nome, plano e limites de linhas com `If-Match`, validação de duplicidade e modal frontend. **Refs:** FR-030, FR-029. **Paths:** `src/WhatsAppAI.Domain/Identity/Tenant.cs`, `src/WhatsAppAI.WebApi/Admin/AdminTenantEndpoints.cs`, `apps/web/src/features/admin/tenants/`, `tests/WhatsAppAI.UnitTests/Identity/TenantTests.cs`. **Depends:** T166. **Aceite:** edição salva e incrementa a versão; conflito de versão, nome/slug duplicado e valores inválidos são rejeitados; credenciais e owner permanecem intactos.
- [X] **T168** Implementar slots independentes de linhas API oficial e QR Code, com sessões QR nomeadas, persistência por canal/linha, quota e seleção na página da empresa. **Refs:** FR-031, FR-029. **Paths:** `src/WhatsAppAI.Domain/Integrations/WhatsAppAccount.cs`, `src/WhatsAppAI.Infrastructure/WhatsApp/`, `src/WhatsAppAI.WebApi/Integrations/WhatsAppEndpoints.cs`, `apps/web/src/features/integrations/whatsapp/`, `services/whatsapp-web/server.mjs`. **Depends:** T166, T167. **Aceite:** cada slot contratado aparece, QR usa sessão própria, API salva sem sobrescrever outra linha e slot fora da quota é rejeitado.
- [X] **T169** Implementar limite configurável de Operators por tenant, enforcement no cadastro e indicador de uso na empresa. **Refs:** FR-032, US-009. **Paths:** `src/WhatsAppAI.Domain/Identity/Tenant.cs`, `src/WhatsAppAI.WebApi/Operators/OperatorEndpoints.cs`, `apps/web/src/features/admin/tenants/`, `apps/web/src/features/operators/`. **Depends:** T167. **Aceite:** limite é editável, criação bloqueia ao atingir a quota, `0` é ilimitado e a UI informa usados/limite.
- [X] **T170** Implementar atribuição de linha por Operator, validação de quota e exposição no `/auth/me`/dashboard. **Refs:** FR-033, FR-031. **Paths:** `src/WhatsAppAI.Domain/Identity/TenantMembership.cs`, `src/WhatsAppAI.WebApi/Operators/OperatorEndpoints.cs`, `src/WhatsAppAI.WebApi/Auth/AuthEndpoints.cs`, `apps/web/src/features/operators/`, `apps/web/src/features/dashboard/`. **Depends:** T168, T169. **Aceite:** TenantOwner atribui/remove linha do tenant corrente; operador vê a linha atribuída após autenticar; atribuição fora da quota falha e tenants não se cruzam.
- [X] **T171** Configurar filas permitidas na IA e transferir automaticamente a conversa conforme seleção/intenção do cliente. **Refs:** FR-036, BR-016, FR-AI-008. **Paths:** `src/WhatsAppAI.Domain/Integrations/AiProviderCredential.cs`, `src/WhatsAppAI.Infrastructure/Workers/AiOrchestrationWorker.cs`, `src/WhatsAppAI.WebApi/Integrations/AiProviderEndpoints.cs`, `apps/web/src/features/integrations/ai/AiInstructionsPage.tsx`, migration e testes. **Depends:** T170. **Aceite:** somente filas ativas selecionadas do tenant entram no prompt; resposta válida da IA atribui a fila e muda a conversa para `Human`; fila inválida ou cruzada é ignorada.
- [X] **T172** Configurar tags permitidas na IA e categorizar automaticamente o contato. **Refs:** FR-037, BR-017, FR-AI-009. **Paths:** credencial/configuração IA, contexto, provedores, worker, migration, frontend e testes. **Depends:** T171. **Aceite:** somente tags ativas selecionadas do tenant entram no prompt; nomes válidos retornados pela IA são adicionados ao contato idempotentemente; tags inválidas, desativadas ou cruzadas são ignoradas; nenhuma tag é removida.
- [X] **T173** Padronizar persistência em PostgreSQL via Npgsql, usando Supabase gerenciado e PostgreSQL Docker na produção própria. **Refs:** NFR-005, NFR-006, NFR-007, ADR-0008. **Paths:** configuração, migrations, Compose, CI, testes e runbooks. **Aceite:** MySQL não participa do runtime; build e testes usam PostgreSQL; conexão contém apenas configuração externa.
- [X] **T174** Permitir atribuir uma fila específica ao Operator, mantendo atendimento geral como padrão e aplicando a restrição no backend. **Refs:** US-009, FR-038, BR-018, NFR-006. **Paths:** `src/WhatsAppAI.Domain/Identity/TenantMembership.cs`, persistência/migration, endpoints de Operators e Conversations, `apps/web/src/features/operators/`, testes. **Depends:** T170, T171. **Aceite:** TenantOwner atribui/remove somente fila ativa do próprio tenant; Operator com fila específica lista, abre e responde apenas conversas dessa fila; sem atribuição mantém o comportamento geral atual.
- [X] **T175** Importar contatos por `.csv` ou `.xlsx` com layout `nome`/`contato`, validação parcial, deduplicação e relatório por linha. **Refs:** US-010, FR-039, BR-019, NFR-006, NFR-008. **Paths:** `src/WhatsAppAI.Application/Contacts/`, `src/WhatsAppAI.Infrastructure/Contacts/`, `src/WhatsAppAI.WebApi/Contacts/ContactEndpoints.cs`, `apps/web/src/features/contacts/`, contrato e testes. **Depends:** T008, T016, T029. **Aceite:** somente TenantOwner importa no tenant corrente; arquivos e cabeçalhos inválidos são rejeitados; linhas válidas são persistidas, duplicadas não sobrescrevem contatos e erros não expõem números no relatório.
- [X] **T176** Exibir ao PlatformAdmin a capacidade da instalação por clientes, linhas cadastradas e operadores, com limites configuráveis e alerta de migração. **Refs:** US-011, FR-040, FR-041, BR-020. **Paths:** `src/WhatsAppAI.Application/Administration/`, `src/WhatsAppAI.Infrastructure/Administration/`, `src/WhatsAppAI.WebApi/Admin/`, `apps/web/src/features/admin/tenants/`, configuração e testes. **Depends:** T166, T169. **Aceite:** somente PlatformAdmin acessa os totais; tenants encerrados não contam; os padrões 25/40/90 podem ser sobrescritos por ambiente; atingir qualquer limite exibe alerta de migração.
- [X] **T177** Modelar STAR, FLOW e SCALA com recursos e padrões comerciais, mantendo BOT/IA_BOT como legados não selecionáveis. **Refs:** US-012, FR-042, FR-043, BR-021. **Paths:** domínio Identity, persistência, migration, API de planos e testes.
- [X] **T178** Provisionar linhas/Operators pelo plano e permitir franquia mensal de IA personalizada por tenant com `If-Match`. **Refs:** US-012, FR-042, FR-044, BR-022. **Paths:** Tenant, endpoints Admin, tela de empresas, auth e testes. **Depends:** T177.
- [X] **T179** Contabilizar respostas de IA e bloquear com handoff/fallback seguro ao atingir a franquia. **Refs:** US-012, FR-045, BR-022. **Paths:** worker de IA, UsageLedger e testes. **Depends:** T178.
- [X] **T180** Aplicar permissões de plano no login, navegação e endpoints dos recursos implementados. **Refs:** US-012, FR-043, BR-021. **Paths:** auth, validação de plano, BOT/tags/filas e frontend. **Depends:** T177.
- [X] **T181** Tornar o toggle de IA concorrente com `If-Match-Bot` e refletir a versão no frontend. **Refs:** US-012, FR-043, BR-021. **Paths:** `src/WhatsAppAI.WebApi/Integrations/AiProviderEndpoints.cs`, `apps/web/src/features/integrations/ai/AiConfigPage.tsx`, testes de integração. **Depends:** T180. **Aceite:** header ausente ou versão obsoleta é rejeitado; somente a versão corrente altera o modo e o estado da IA.
- [X] **T182** Expor consumo real de tokens por tenant/provedor/modelo, manter respostas como franquia comercial e mostrar saldo/alerta de 80% no dashboard para orientar recarga administrativa. **Refs:** US-006, US-013, FR-044, FR-046, FR-047, FR-048, BR-022. **Paths:** `AdminTenantEndpoints`, tela de empresas, dashboard, ledger de uso e especificações. **Depends:** T181. **Aceite:** o PlatformAdmin consulta entrada/saída/total e distribuição mensal, libera/renova o pacote com `If-Match`, e o TenantOwner vê consumo e aviso sem que tokens sejam uma segunda quota operacional.
- [X] **T183** Criar catálogo global versionado de preços por provedor/modelo, calcular custo de entrada/saída no worker e persistir moeda e versão no `UsageLedger`. **Refs:** FR-018, FR-048, FR-049, FR-AI-011, NFR-006. **Paths:** domínio/infraestrutura `Usage`, migration, endpoint administrativo de preços, worker de IA e testes. **Depends:** T182. **Aceite:** somente a versão vigente no instante da resposta é aplicada; ausência de preço mantém tokens registrados sem custo; versões anteriores permanecem consultáveis e nenhum segredo é exposto.
