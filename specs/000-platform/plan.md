# Plano técnico

**Spec relacionada:** `spec.md`  
**Constituição:** `.specify/memory/constitution.md`

## 1. Arquitetura escolhida

Monólito modular com frontend separado e um único backend implantável. O backend expõe HTTP/SignalR, executa workers internos e acessa PostgreSQL. Limites de módulos permitem extração futura, mas não criam custo distribuído antecipadamente.

### Módulos

- **Identity & Tenancy:** autenticação, papéis, tenant corrente e administração.
- **Integrations:** configuração Meta/OpenAI, teste de conexão e segredos.
- **Messaging:** webhook, contatos, conversas, mensagens, Inbox/Outbox e status.
- **Automation:** política, contexto, interação de IA e handoff.
- **Knowledge:** conteúdo ativo que fundamenta respostas.
- **Usage & Audit:** unidades, estimativas, auditoria e métricas.

## 2. Stack

| Área | Escolha | Motivo |
|---|---|---|
| Backend | .NET 10 LTS / ASP.NET Core | LTS atual, bom suporte a API, workers e SignalR |
| ORM | EF Core 10 + Npgsql | migrations e integração madura com PostgreSQL |
| Frontend | React 19.2 + TypeScript + Vite | SPA simples, tipada e de ciclo rápido |
| UI data | TanStack Query | cache e estados de servidor sem store global excessiva |
| Banco | PostgreSQL 18 | dados transacionais, JSONB e filas duráveis iniciais |
| Tempo real | SignalR | integração nativa e grupos por tenant |
| IA | OpenAI Responses API | interface atual com saída estruturada e uso auditável |
| WhatsApp | Meta Graph/Cloud API | canal oficial e suportado |
| Testes | xUnit, Testcontainers, Vitest, Playwright | pirâmide completa e ambiente realista |
| Local | Docker Compose | PostgreSQL e dependências reproduzíveis |

Versões menores devem ser fixadas no bootstrap e atualizadas de forma deliberada.

## 3. Estrutura prevista

```text
apps/
  web/
src/
  WhatsAppAI.Domain/
  WhatsAppAI.Application/
  WhatsAppAI.Infrastructure/
  WhatsAppAI.WebApi/
tests/
  WhatsAppAI.UnitTests/
  WhatsAppAI.IntegrationTests/
  WhatsAppAI.ArchitectureTests/
  e2e/
deploy/
docs/
specs/
```

Cada módulo possui casos de uso em `Application`, entidades/regras em `Domain`, adaptadores em `Infrastructure` e endpoints finos em `WebApi`. Não criar camada genérica de repository sobre EF Core sem regra que a justifique.

## 4. Interfaces de borda

- `IWhatsAppClient`: enviar mensagem, baixar metadados de mídia e verificar conexão.
- `IAiProvider`: gerar `AiDecision` estruturada e verificar conexão.
- `ISecretStore`: gravar, recuperar para uso interno, rotacionar e remover segredo.
- `IClock`: tornar janela de 24 horas e expiração testáveis.
- `ICurrentTenant`: transportar contexto autenticado sem aceitar `TenantId` arbitrário do cliente.
- `IOutboxDispatcher`: despachar operações externas idempotentes.

## 5. Fluxos críticos

### Entrada

1. Webhook valida desafio/assinatura e identifica a conta conectada.
2. Salva `WebhookEvent` com chave única e responde rapidamente.
3. Worker converte payload em contato, conversa e mensagem dentro de transação.
4. Commit grava também evento de domínio/outbox para SignalR/automação.
5. Automação lê estado atual, gera decisão, revalida versão/modo/janela e enfileira envio.

### Saída

1. Caso de uso valida autorização, modo, janela e conteúdo.
2. Cria `Message` em `Queued` e `OutboxMessage` na mesma transação.
3. Worker chama Meta com correlação e idempotência local.
4. Atualiza provider ID/status; webhooks posteriores avançam o status.

### Handoff

O comando de assumir conversa incrementa `Version`, muda para `Human` e registra auditoria. Qualquer decisão de IA baseada em versão anterior é descartada por **BR-004/BR-010**.

## 6. Dados e transações

- IDs internos usam UUID/UUIDv7 quando suportado pela aplicação.
- Horários usam UTC (`timestamptz`).
- Todas as tabelas tenant-owned incluem `tenant_id` e índices iniciados por ele.
- `WebhookEvent` implementa Inbox; `OutboxMessage` implementa envio durável.
- EF Core aplica filtro global como defesa adicional, mas casos de uso continuam exigindo tenant explícito do contexto.
- Índices únicos incluem tenant quando o identificador externo não é globalmente garantido.

## 7. Segurança

- ASP.NET Core Identity com sessão em cookie HttpOnly/Secure; proteção CSRF em mutações.
- Webhook tem autenticação própria, rate limit e limite de payload.
- Produção usa cofre gerenciado via `ISecretStore`; desenvolvimento usa user-secrets/variáveis, nunca `appsettings.json` versionado.
- Telefones são mascarados em logs; conteúdo de mensagens não entra em log padrão.
- CORS é allowlist; headers de segurança e TLS são obrigatórios.
- PlatformAdmin opera em contexto selecionado e auditado, sem bypass global implícito.

## 8. Observabilidade e operação

- Logs JSON com `correlation_id`, `tenant_id` pseudonimizado, `conversation_id`, operação e resultado.
- Métricas: latência/erro de webhook, profundidade e idade de filas, tentativas, envios, handoffs, latência/tokens da IA.
- Health checks separados em liveness e readiness.
- OpenTelemetry para traces; exportador definido no deploy.
- Retry exponencial com jitter apenas para falhas transitórias; dead-letter lógico após limite.

## 9. Estratégia de entrega

Implementar fatias verticais em ordem de `tasks.md`. A primeira demo funcional termina na Fase 4 (entrada + inbox + resposta humana). A IA entra somente após o caminho humano e a recuperação de falhas estarem estáveis.

## 10. Gates da constituição

| Princípio | Como o plano atende |
|---|---|
| Simplicidade | monólito, escopo inbound/service, sem orquestrador externo |
| Integrações oficiais | adaptadores diretos Meta/OpenAI |
| Controle humano | estados de conversa, versão e revalidação |
| Isolamento | tenant em dados, auth, SignalR, jobs e testes |
| Observável | Inbox/Outbox, correlação, métricas e runbooks |
| Proporcional | PostgreSQL central; extração condicionada a métricas |
| Especificação executável | IDs rastreados em contrato, tarefas e testes |
