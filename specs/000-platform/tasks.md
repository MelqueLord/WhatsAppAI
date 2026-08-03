# Backlog de implementação

## Regras

- Execute em ordem de dependência; `[P]` indica paralelizável somente depois das dependências.
- Cada tarefa termina com build/testes e commit referenciando o ID.
- Não começar IA antes da entrada, inbox e envio humano estarem estáveis.

## Fase 0 — Bootstrap e qualidade

- [ ] **T001** Criar solution .NET 10, projetos definidos em `plan.md` e `global.json` (**R-001**).
- [ ] **T002** Criar React 19.2 + TypeScript + Vite em `apps/web` (**R-002**).
- [ ] **T003** Configurar PostgreSQL 18 no Docker Compose, EF Core/Npgsql e primeira migration (**R-003**).
- [ ] **T004** [P] Configurar formatadores, analyzers, nullable, warnings e testes de arquitetura.
- [ ] **T005** [P] Configurar CI com restore, lint, build, testes e `git diff --check`.
- [ ] **T006** Criar logging estruturado, correlation ID, Problem Details e health checks (**NFR-008**).

**Marco:** solução vazia compila, sobe localmente e CI passa.

## Fase 1 — Identidade e tenancy

- [ ] **T010** Modelar Tenant, User e TenantMembership; migrations (**FR-002**, **FR-003**).
- [ ] **T011** Implementar ASP.NET Core Identity com cookie seguro e CSRF (**FR-001**).
- [ ] **T012** Implementar `ICurrentTenant`, papéis e seleção explícita de contexto administrativo (**FR-002**, **US-007**).
- [ ] **T013** Aplicar filtros/guardas de tenant e testes negativos cruzando dois tenants (**NFR-006**).
- [ ] **T014** Criar login/logout e shell autenticado da SPA.

**Marco:** dois tenants coexistem e não acessam dados um do outro.

## Fase 2 — Configuração e entrada da Meta

- [ ] **T020** Definir `ISecretStore` e implementações de desenvolvimento/teste (**FR-004**).
- [ ] **T021** Implementar WhatsAppAccount e tela de configuração com campos write-only (**FR-021**, **US-001**).
- [ ] **T022** Implementar `IWhatsAppClient` e teste de conexão sanitizado (**FR-021**).
- [ ] **T023** Implementar GET de verificação e POST com assinatura/limites (**FR-005**).
- [ ] **T024** Persistir WebhookEvent idempotente e responder sem processamento síncrono (**FR-006**, **FR-007**, **NFR-001**).
- [ ] **T025** Implementar worker com lock, retry, backoff e dead-letter lógico (**BR-009**).
- [ ] **T026** Normalizar contatos, conversas, mensagens e status (**FR-008**, **BR-002**).
- [ ] **T027** Criar testes com fixtures oficiais/anônimas, assinatura inválida e reentrega em massa (**SC-002**, **SC-003**).

**Marco:** mensagem real da Meta é persistida uma única vez.

## Fase 3 — Inbox em tempo real

- [ ] **T030** Implementar consultas paginadas de conversas e histórico conforme OpenAPI (**US-002**).
- [ ] **T031** Implementar hub SignalR autenticado com grupo por tenant (**FR-009**).
- [ ] **T032** Criar lista de conversas, painel de mensagens e estados vazios/erro/loading.
- [ ] **T033** Atualizar inbox em tempo real e reconciliar com cache da API (**NFR-002**).
- [ ] **T034** Exibir tipos de mídia básica com fallback seguro para não suportados.
- [ ] **T035** Testar autorização SignalR e tentativa de inscrição em tenant alheio (**NFR-006**).

**Marco:** inbox operacional recebe mensagens em tempo real.

## Fase 4 — Resposta humana e saída durável

- [ ] **T040** Implementar mudança de modo com `If-Match`, versão e HandoffEvent (**FR-011**, **BR-004**).
- [ ] **T041** Implementar regra testável da janela de 24 horas com `IClock` (**FR-012**, **BR-005**).
- [ ] **T042** Criar Message + OutboxMessage na mesma transação e endpoint idempotente (**FR-010**).
- [ ] **T043** Implementar dispatcher Meta, retry seletivo e estados de entrega.
- [ ] **T044** Integrar compositor da inbox com bloqueio/explicação de janela fechada (**US-003**).
- [ ] **T045** Testar concorrência, duplicação, timeout e falha definitiva (**NFR-009**).

**Marco:** operador assume e responde uma conversa real com recuperação de falhas.

## Fase 5 — IA segura

- [ ] **T050** Implementar AiProviderCredential e configuração write-only (**FR-004**, **US-001**).
- [ ] **T051** Definir `IAiProvider`, `AiDecision` e adaptador Responses API com saída estruturada (**FR-014**).
- [ ] **T052** Criar montador de contexto com política, histórico limitado e orçamento de tokens (**FR-013**).
- [ ] **T053** Implementar orquestração após inbound em modo Automatic.
- [ ] **T054** Revalidar modo, janela e `Conversation.version` antes do Outbox (**FR-015**, **BR-010**).
- [ ] **T055** Registrar AiInteraction, tokens, latência e UsageLedger (**FR-016**).
- [ ] **T056** Aplicar regras de handoff de `behavior-policy.md` e circuit breaker local.
- [ ] **T057** Criar avaliações com perguntas conhecidas, ambíguas, adversariais e pedido humano.
- [ ] **T058** Testar corrida “IA gerando enquanto operador assume” (**SC-004**).

**Marco:** IA responde texto seguro e cede controle de forma determinística.

## Fase 6 — Conhecimento e configuração do negócio

- [ ] **T060** Implementar KnowledgeItem, validações, CRUD e auditoria (**FR-017**, **US-005**).
- [ ] **T061** Criar tela de conhecimento com ativação, prioridade e limites.
- [ ] **T062** Integrar seleção determinística de itens ativos ao contexto (**R-005**).
- [ ] **T063** Testar isolamento, limite de contexto e atualização usada na interação seguinte.

**Marco:** proprietário ajusta respostas sem deploy.

## Fase 7 — Uso, auditoria e retenção

- [ ] **T070** Completar UsageLedger para Meta/OpenAI com preços versionados opcionais (**FR-018**, **BR-007**).
- [ ] **T071** Criar painel de unidades/estimativas e disclaimer de fatura (**US-006**).
- [ ] **T072** Implementar AuditLog append-only para ações sensíveis (**FR-019**).
- [ ] **T073** Implementar jobs de retenção e exclusão com aprovação/configuração (**FR-020**).
- [ ] **T074** Testar ausência de segredos/PII em logs, erros e exportações (**NFR-008**).

## Fase 8 — Produção e piloto

- [ ] **T080** Escolher hospedagem/cofre via ADR e implementar `ISecretStore` gerenciado.
- [ ] **T081** Configurar TLS, CORS, headers, rate limits, backup e restore ensaiado.
- [ ] **T082** Exportar métricas/traces, dashboards e alertas de fila/webhook/IA.
- [ ] **T083** Executar testes de carga das metas **NFR-001/002/007**.
- [ ] **T084** Executar threat model e checklist de LGPD com revisão jurídica independente.
- [ ] **T085** Provisionar tenant piloto e medir **SC-001..006** por sete dias.
- [ ] **T086** Registrar incidentes, feedback e ADRs necessários antes de ampliar vendas.

## Backlog posterior, não autorizado no MVP

- Templates utilitários aprovados para janela fechada.
- Múltiplos números/equipes, RAG vetorial, canais adicionais, billing interno e integrações periféricas.
- Redis, broker ou microsserviços somente após critérios de `research.md`.
