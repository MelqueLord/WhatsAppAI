# Guia de funcionamento implementado

**Atualizado em:** 2026-08-28  
**Escopo:** comportamento atualmente implementado no monólito WhatsAppAI.  
**Fonte:** código, testes e migrações presentes no repositório.

Este documento é um mapa operacional para consulta. Ele descreve o que já funciona, os pontos de entrada e as limitações conhecidas. A especificação do produto continua sendo a fonte de decisão para mudanças futuras.

## 1. Limites do produto

- O MVP automatiza atendimento por WhatsApp; não é CRM de funil, agenda, catálogo, campanha ou construtor de bot.
- BOT e IA são produtos separados. O plano **BOT** pode operar sem provedor ou credencial de IA; o plano **IA + BOT** libera os dois módulos.
- As telas `/bot` e `/integrations/ai` permanecem separadas. Ambas alteram a configuração operacional do tenant, mas não duplicam o fluxo de atendimento.
- O modo do `BotConfiguration` é a fonte única de exclusividade: `Manual`, `SimpleAutoReply` ou `AiPowered`. Ativar BOT simples desativa IA operacional; ativar IA coloca o BOT em `AiPowered`.
- A base de conhecimento usa recuperação lexical determinística (termos da mensagem, título com peso 3 e conteúdo com peso 1). RAG vetorial não faz parte do MVP.

Referências: `specs/000-platform/spec.md`, `docs/ai/behavior-policy.md` e `docs/architecture/architecture.md`.

## 2. Acesso e isolamento

- Toda requisição de negócio passa pelo tenant corrente (`RequireTenantContext`) e consultas são filtradas por `TenantId`.
- Configurações de BOT, IA, credenciais, diretrizes e simulação exigem atualmente `TenantOwner` no backend.
- Operadores podem consultar e atuar apenas nas filas/linhas autorizadas; `AssignedQueueId = null` significa atendimento geral.
- Segredos são lidos/escritos por `ISecretStore`; chaves não são persistidas em texto puro nem incluídas em logs.
- Webhooks e ações de envio passam pelo backend; o modelo nunca chama a Meta diretamente.
- Conteúdo de prompt, PII e tokens não devem ser registrados sem mascaramento.

Arquivos principais: `src/WhatsAppAI.WebApi/TenantContextExtensions.cs`, endpoints por módulo, filtros do `AppDbContext` e `ISecretStore`.

## 3. Tela e configuração do BOT

Frontend: `apps/web/src/features/bot/BotConfigPage.tsx`  
Backend: `src/WhatsAppAI.WebApi/Bot/BotConfigurationEndpoints.cs`  
Domínio: `src/WhatsAppAI.Domain/Integrations/BotConfiguration.cs`

### O que é configurado

- modo (`Manual`, `SimpleAutoReply`, `AiPowered`);
- saudação inicial e mensagem de retorno;
- mensagem offline, fallback, mídia, handoff e transferência de fila;
- etapas do fluxo/menu e palavras-chave;
- limiar de confiança compartilhado com o runtime de IA;
- habilitação/desabilitação do BOT.

### Regras de gravação

- `GET /api/bot-config` carrega configuração e `Version`.
- Alterações em `POST /api/bot-config`, `/mode`, `/messages` e `/toggle` exigem `If-Match` com a versão atual.
- Versão obsoleta não sobrescreve alteração mais nova.
- O backend valida plano e modo antes de salvar.
- A prévia do fluxo na tela é local; não envia mensagem.

## 4. Tela e configuração da IA

Frontend: `apps/web/src/features/integrations/ai/AiConfigPage.tsx`  
Backend: `src/WhatsAppAI.WebApi/Integrations/AiProviderEndpoints.cs`

### Configurações disponíveis

- provedor e modelo (catálogo atual: OpenAI, Gemini, Anthropic, Xiaomi, Grok e Groq);
- credencial armazenada no `ISecretStore`;
- ativação da IA;
- diretriz livre e grupos estruturados de comportamento, segurança e handoff;
- `MaxTokensPerResponse` (uma fonte usada pelo runtime);
- um único `ConfidenceThreshold` do tenant, armazenado em `BotConfiguration`;
- filas e tags autorizadas para roteamento/categorização;
- simulação antes da ativação.

### Controle de concorrência

- Salvar provedor/credencial exige `If-Match` da configuração de IA.
- Salvar diretrizes e limiar exige `If-Match`; quando o limiar afeta BOT, exige também `If-Match-Bot`.
- A API responde conflito/erro de versão sem sobrescrever dados mais novos.
- O toggle de IA ainda é uma operação separada da gravação de diretrizes e não usa o mesmo `If-Match` transacional.

### Simulação

- `POST /api/integrations/ai/simulate` pode chamar o provedor real.
- Retorna decisão, confiança e motivo de handoff/fallback.
- Não cria mensagem, outbox, alteração de conversa nem evento operacional.
- Registra somente auditoria técnica `AI.Simulation`, com dados sanitizados.

## 5. Fluxo de entrada e decisão automática

Worker: `src/WhatsAppAI.Infrastructure/Workers/AiOrchestrationWorker.cs`  
Guard: `src/WhatsAppAI.Infrastructure/Workers/AiReplyDeliveryGuard.cs`

1. A mensagem inbound é processada pelo worker durável.
2. A conversa é carregada; se não existir, o inbound é marcado como processado e não fica em loop.
3. O worker exige modo `Automatic`, tenant ativo, configuração habilitada e modo compatível.
4. O worker captura a versão da conversa e verifica a janela de 24 horas.
5. Em `Manual`, marca o inbound e não responde.
6. Em `SimpleAutoReply`, escolhe resposta por fluxo/palavra-chave, saudação, retorno, fallback ou padrão configurado.
7. Em `AiPowered`, valida plano, credencial, segredo, modelo, autorização de tratamento de dados e orçamento.
8. Monta contexto, chama o provedor, interpreta resposta estruturada e aplica as políticas de comportamento, fila e tags.
9. Persiste somente metadados sanitizados de interação/uso.
10. Antes de criar a mensagem e o outbox, recarrega a conversa e revalida todas as condições.
11. Só depois cria mensagem outbound e item de outbox com idempotência.
12. Marca o inbound como processado.

## 6. Revalidação antes de enviar

Nenhuma resposta automática é enviada se qualquer item abaixo tiver mudado entre a decisão e o enfileiramento:

- versão da conversa;
- modo atual (incluindo takeover humano);
- janela de atendimento de 24 horas;
- handoff humano concorrente.

Essa proteção é aplicada ao caminho de IA e ao `SimpleAutoReply`, imediatamente antes da criação de mensagem/outbox.

## 7. Confiança, schema, fallback e handoff

Políticas: `src/WhatsAppAI.Application/Automation/Policy/BehaviorPolicy.cs` e `AiGuidelinePolicy.cs`.

- Existe um único limiar configurável por tenant. A decisão usa `confidence < threshold`, `== threshold` e `> threshold` de forma determinística.
- Resposta fora do schema, JSON inválido, resposta vazia ou `Reply` sem conteúdo nunca é enviada ao cliente.
- Resposta inválida/vazia segue o fallback/handoff configurado; o motivo é registrado.
- Fallback e handoff automáticos são bloqueados fora da janela de 24 horas.
- Handoff automático muda o modo da conversa para `Human` e grava `HandoffEvent`.
- Motivos cobertos incluem: baixa confiança, pedido do cliente, tema sensível, fora de escopo, escalonamento, reclamação, reembolso, tema jurídico, resposta inválida, seleção de fila, IA indisponível, tratamento de dados não autorizado, orçamento excedido, cota excedida e retries excedidos.
- A mensagem usa `HandoffMessage` do tenant; se ausente, usa `FallbackMessage`; na ausência de ambos existe uma mensagem padrão mínima.

## 8. Falhas e retries

- Provedor, credencial, modelo ou configuração obrigatória ausente não deixa o inbound em reprocessamento infinito.
- Falhas transitórias usam retry limitado; ao exceder o limite o estado é finalizado e segue fallback/handoff permitido.
- O caminho de cota e erro de provedor também respeita janela de 24 horas.
- O outbox usa chave determinística (por exemplo, `simple-auto-reply:{message.Id}`) para evitar duplicidade.
- Exceções e tentativas permanecem observáveis por logs/telemetria sem incluir segredos ou conteúdo sensível.

## 9. Base de conhecimento e contexto (RAG lexical)

Domínio: `src/WhatsAppAI.Domain/Knowledge/KnowledgeItem.cs`  
API/UI: `src/WhatsAppAI.WebApi/Knowledge/KnowledgeEndpoints.cs`, `apps/web/src/features/knowledge/KnowledgePage.tsx`  
Recuperação: `src/WhatsAppAI.Application/Automation/Context/ContextAssembler.cs`

- Itens têm título, conteúdo, prioridade, ativo/inativo e versão.
- CRUD, ativação e desativação exigem `If-Match` nas alterações.
- Somente itens ativos do tenant entram no contexto.
- A consulta usa a mensagem mais recente do usuário; até seis itens são selecionados por sobreposição de termos, priorizando título e depois conteúdo, com desempate por prioridade/data.
- Se não houver correspondência, há fallback determinístico para itens ativos por prioridade.
- Histórico é limitado a seis mensagens e 360 caracteres por mensagem; contexto de conhecimento a 9.000 caracteres.
- Texto é sanitizado antes de entrar no prompt.

## 10. Handoff, filas e operadores

- Operadores podem ter uma fila específica ou atendimento geral.
- Listagem, abertura, resposta e transferência validam o escopo da fila/linha do operador.
- O roteamento de IA só escolhe filas/tags autorizadas e existentes no tenant.
- Mudança manual para humano registra `HandoffEvent` com motivo suportado.
- Mudanças de modo de conversa exigem `If-Match` e não substituem takeover humano concorrente.

Arquivos: `TenantMembership`, `OperatorEndpoints`, `ConversationQueries`, `ConversationEndpoints` e `ConversationModeEndpoints`.

## 11. Importação de contatos

Frontend: `apps/web/src/features/contacts/ContactsPage.tsx`  
Backend: `src/WhatsAppAI.WebApi/Contacts/ContactEndpoints.cs`  
Serviço: `src/WhatsAppAI.Application/Contacts/ContactImportService.cs`

- Aceita CSV/XLSX de até 2 MB e 5.000 linhas.
- Layout mínimo: `nome` e `contato` (número).
- Normaliza números, valida linhas, deduplica e preserva resultados parciais.
- Tudo é gravado no tenant corrente; números não são expostos em mensagens de erro/resultados.

## 12. Auditoria, uso e observabilidade

- `AuditLog`: alterações de configuração, ações de segurança e auditoria técnica de simulação.
- `AiInteraction`: decisão e metadados sanitizados da chamada de IA.
- `UsageLedger`: consumo e limites do tenant.
- `HandoffEvent`: transições automáticas e manuais para atendimento humano.
- Inbox/Outbox e workers fornecem idempotência, retry e rastreabilidade por correlação.
- Métricas, health checks e alertas estão descritos em `docs/runbooks/observability.md`.

## 13. Demais módulos implementados

### Identidade, tenants e administração

- Login/logout por cookie seguro, antiforgery, `GET /auth/me` e invalidação por estado/security stamp.
- Platform Admin cria, suspende e reativa tenants; TenantOwner recebe convite de uso único com expiração.
- TenantOwner convida, reenvia, desativa e reativa operadores sem cruzar tenants.
- Nome, plano, quotas e limites administrativos usam versionamento otimista.

### WhatsApp e entrada de mensagens

- Conta oficial Cloud API por tenant, configuração write-only e teste de conexão sanitizado.
- Verificação/assinatura de webhook, envelope idempotente, payload protegido, classificação de eventos desconhecidos e reprocessamento auditado.
- Normalização de contato, conversa, mensagem e status; apenas inbound do cliente renova a janela de 24 horas.
- Linhas independentes por API oficial e QR Code, com sessões nomeadas, quotas e seleção por linha.

### Inbox, mídia e resposta humana

- Inbox paginada por cursor, mensagens em tempo real via SignalR por tenant e proxy autenticado de mídia.
- Operador assume/pausa conversa com controle de modo, versão, janela e `HandoffEvent`.
- Mensagem e `OutboxMessage` são criados na mesma transação; envio usa idempotência, retry seletivo e estados de entrega.

### Planos, filas e classificação

- Planos controlam limites e funcionalidades; endpoints e worker bloqueiam IA quando o plano não autoriza.
- Filas e tags ativas podem ser autorizadas para a IA; referências inválidas ou de outro tenant são ignoradas.
- Operador pode ser restrito a uma fila ou permanecer em atendimento geral; a restrição é aplicada no backend.

### Uso, privacidade e broadcast

- Painel de uso separa provedor/período e apresenta estimativa, não fatura.
- Retenção, exclusão operacional, controles de privacidade e checklist LGPD estão previstos nos runbooks de segurança.
- Existe módulo de broadcast com listagem, detalhe, criação, disparo, cancelamento e exclusão; ele segue permissões, tenant e outbox do backend.

## 14. Persistência e migrations

Banco padrão: PostgreSQL via Npgsql; Supabase é uma opção de hospedagem. As alterações de modelo ficam em `src/WhatsAppAI.Infrastructure/Migrations` e incluem, entre outras, configuração de IA, filas de IA, atribuição de linhas/filas, controles de privacidade, retry de mensagens de IA, limiar de confiança e baseline PostgreSQL.

Em deploy, a aplicação deve aplicar as migrations pendentes antes de iniciar o worker. Uma falha de `PendingModelChangesWarning` indica que o modelo e o snapshot não estão sincronizados e requer nova migration antes do `Migrate`.

## 15. Testes existentes

- Unitários de domínio para configuração BOT, conhecimento, memberships, políticas e handoff.
- Unitários do worker para fallback/handoff, retries, janela, versão, takeover e mensagens configuradas.
- Unitários do contexto para seleção lexical e fallback por prioridade.
- Testes de endpoints/UI para `If-Match`, planos, toggle, simulação, BOT e importação.
- Testes de isolamento por tenant e contratos/integração ficam na estratégia em `docs/testing/strategy.md`.

## 16. Limitações conhecidas para futuras correções

Estas são lacunas reais do estado atual, não comportamentos prometidos:

- toggle de IA ainda pode concorrer sem `If-Match` atômico;
- atualização de instruções/credencial e limiar pode falhar parcialmente quando há conflito entre versões distintas;
- gravação de mudança de modo e `HandoffEvent` ocorre em operações separadas;
- RAG vetorial, re-ranking semântico e citações de fonte não estão implementados;
- a tela não oferece uma visão operacional completa de todas as interações, handoffs e uso;
- testes de integração dependem de Docker/Testcontainers no ambiente de execução.

Para alterar qualquer regra crítica, atualizar também a especificação/ADR correspondente e os testes de regressão.

## 17. Onde consultar o detalhe

| Assunto | Documento/arquivo |
|---|---|
| Regras de comportamento da IA | `docs/ai/behavior-policy.md` |
| Arquitetura e limites | `docs/architecture/architecture.md` |
| Segurança/LGPD | `docs/security/lgpd-checklist.md` e `docs/security/threat-model.md` |
| Observabilidade | `docs/runbooks/observability.md` |
| Estratégia de testes | `docs/testing/strategy.md` |
| Contrato do produto | `specs/000-platform/spec.md` |
| Plano técnico | `specs/000-platform/plan.md` |
| Tarefas e rastreabilidade | `specs/000-platform/tasks.md` |
