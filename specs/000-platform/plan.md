# Plano técnico

**Spec relacionada:** `spec.md`  
**Constituição:** `.specify/memory/constitution.md`

## 1. Arquitetura escolhida

Monólito modular com frontend separado e um único backend implantável. O backend expõe HTTP/SignalR, executa workers internos e acessa PostgreSQL via Npgsql. Supabase hospeda o banco gerenciado e PostgreSQL em Docker atende a produção própria.

### Módulos

- **Identity & Tenancy:** autenticação, papéis, tenant corrente e administração.
- **Integrations:** configuração Meta Cloud, WhatsApp Web/Baileys, OpenAI, teste de conexão e segredos.
- **Messaging:** webhook, contatos, conversas, mensagens, Inbox/Outbox e status.
- **Automation:** política, contexto, interação de IA e handoff.
- **Knowledge:** conteúdo ativo que fundamenta respostas.
- **Usage & Audit:** unidades, estimativas, auditoria e métricas.

## 2. Stack

| Área | Escolha | Motivo |
|---|---|---|
| Backend | .NET 10 LTS / ASP.NET Core | LTS atual, bom suporte a API, workers e SignalR |
| ORM | EF Core 10 + Npgsql | migrations e integração PostgreSQL |
| Frontend | React 19.2 + TypeScript + Vite | SPA simples, tipada e de ciclo rápido |
| UI data | TanStack Query | cache e estados de servidor sem store global excessiva |
| Banco | PostgreSQL | Supabase gerenciado ou container Docker na Hostinger |
| Tempo real | SignalR | integração nativa e grupos por tenant |
| IA | OpenAI Responses API | interface atual com saída estruturada e uso auditável |
| WhatsApp | Meta Graph/Cloud API e Baileys/WhatsApp Web por QR | Cloud é o canal oficial; QR atende linhas conectadas por sessão Baileys |
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
- `IMediaGateway`: obter mídia da Meta para proxy autenticado sem expor credenciais ou URL privada.

## 5. Fluxos críticos

### Entrada

1. O GET valida o challenge com o verify token global; o POST lê o corpo sem desserialização destrutiva e valida a assinatura com o `app_secret` global do único Meta App. Ambos são recuperados pelo `ISecretStore`.
2. Somente depois da assinatura válida, extrai `phone_number_id`, resolve a conta/tenant e salva `WebhookEvent` com chave única.
3. O evento guarda envelope operacional sanitizado separado do payload original cifrado e restrito; eventos desconhecidos são classificados e permanecem consultáveis/reprocessáveis.
4. O endpoint responde rapidamente, sem esperar IA, e o worker converte payload reconhecido em contato, conversa e mensagem dentro de transação.
5. Commit grava também evento de domínio/outbox para SignalR/automação.
6. Automação lê estado atual, gera decisão, revalida versão/modo/janela e enfileira envio.

### Saída

1. Caso de uso valida autorização, modo, janela e conteúdo.
2. Cria `Message` em `Queued` e `OutboxMessage` na mesma transação.
3. Worker chama o canal da linha (Meta Cloud ou ponte Baileys) com correlação e idempotência local.
4. Atualiza provider ID/status; webhooks posteriores avançam o status.

### Handoff

O comando de assumir conversa incrementa `Version`, muda para `Human` e registra auditoria. Qualquer decisão de IA baseada em versão anterior é descartada por **BR-004/BR-010**.

### Mídia

1. A mensagem persiste apenas identificador e metadados seguros da mídia.
2. A SPA solicita a mídia a endpoint autenticado da WebApi com conversa e mensagem no tenant corrente.
3. A WebApi autoriza o acesso, usa internamente a credencial do canal do tenant para obter o conteúdo e transmite o arquivo com limites de tipo/tamanho.
4. Credencial e URL privada do provedor nunca são enviados ao navegador (**FR-023**).

## 6. Dados e transações

- IDs internos usam UUID/UUIDv7 quando suportado pela aplicação.
- Horários usam UTC (`timestamptz`).
- Todas as tabelas tenant-owned incluem `tenant_id` e índices iniciados por ele.
- `WebhookEvent` implementa Inbox; `OutboxMessage` implementa envio durável.
- EF Core aplica filtro global como defesa adicional, mas casos de uso continuam exigindo tenant explícito do contexto.
- Índices únicos incluem tenant quando o identificador externo não é globalmente garantido.

## 7. Segurança

- Frontend e backend usam o mesmo site. ASP.NET Core Identity usa cookie `HttpOnly`, `Secure` e `SameSite=Lax` em produção.
- Um endpoint de bootstrap emite token antiforgery; toda mutação autenticada deve enviá-lo em `X-CSRF-TOKEN`. Login é público (`security: []`), mas também exige o token antiforgery.
- O webhook Cloud tem autenticação própria com `app_secret` e verify token globais no cofre; a ponte Baileys usa segredo próprio. Ambos têm rate limit e limite de payload antes da resolução pelo `phone_number_id`.
- Produção usa cofre gerenciado via `ISecretStore`; desenvolvimento usa user-secrets/variáveis, nunca `appsettings.json` versionado.
- Telefones são mascarados em logs; conteúdo de mensagens não entra em log padrão.
- CORS é allowlist; headers de segurança e TLS são obrigatórios.
- PlatformAdmin opera em contexto selecionado e auditado, sem bypass global implícito.
- O `AuditLog` usa identidade de banco sem permissão de `UPDATE`/`DELETE`; correções geram novo evento, nunca alteração do anterior.
- Prompt completo nunca é persistido; `AiInteraction` recebe somente metadados operacionais sanitizados.

## 8. Observabilidade e operação

- Logs JSON com `correlation_id`, `tenant_id` pseudonimizado, `conversation_id`, operação e resultado.
- Métricas: latência/erro de webhook, profundidade e idade de filas, tentativas, envios, handoffs, latência/tokens da IA.
- Health checks separados em liveness e readiness.
- OpenTelemetry para traces; exportador definido no deploy.
- Retry exponencial com jitter apenas para falhas transitórias; dead-letter lógico após limite.
- O SLI mensal de **NFR-004** divide respostas elegíveis concluídas sem 5xx/timeout da plataforma pelo total de requisições válidas recebidas; falhas Meta/OpenAI recebem dimensão separada sem sair do total e manutenção não é excluída.
- O ensaio de recuperação restaura backup com ponto de no máximo 24 horas e mede até 4 horas da declaração do incidente ao smoke test aprovado (**NFR-005**).
- A validação de IA usa no mínimo 100 requisições elegíveis e separa fila, aplicação e provedor para comprovar **NFR-003**.

## 9. Estratégia de entrega

Implementar fatias verticais em ordem de `tasks.md`. A primeira demo funcional termina na Fase 4 (entrada + inbox + resposta humana). A IA entra somente após o caminho humano e a recuperação de falhas estarem estáveis.

## 10. Gates da constituição

| Princípio | Como o plano atende |
|---|---|
| Simplicidade | monólito, escopo inbound/service, sem orquestrador externo |
| Integrações | adaptadores diretos Meta/OpenAI e ponte QR Baileys |
| Controle humano | estados de conversa, versão e revalidação |
| Isolamento | tenant em dados, auth, SignalR, jobs e testes |
| Observável | Inbox/Outbox, correlação, métricas e runbooks |
| Proporcional | PostgreSQL central; extração condicionada a métricas |
| Especificação executável | IDs rastreados em contrato, tarefas e testes |

## 11. Decisões arquiteturais vigentes

Este plano reutiliza, sem duplicar, os ADRs aceitos:

- `docs/architecture/adr/0001-modular-monolith.md`;
- `docs/architecture/adr/0002-official-whatsapp-cloud-api.md`;
- `docs/architecture/adr/0003-customer-owned-provider-billing.md`;
- `docs/architecture/adr/0004-no-n8n-core.md`;
- `docs/architecture/adr/0005-postgres-inbox-outbox.md`;
- `docs/architecture/adr/0006-hosting-and-secrets.md`;
- `docs/architecture/adr/0009-baileys-production-qr.md`.

A topologia de um Meta App compartilhado especializa ADR-0002/0003 e está registrada em **R-010**. A política normativa de IA é `docs/ai/behavior-policy.md`.

## 12. Correção para prontidão de produção

O incremento de endurecimento e publicação está detalhado em:

- `production-readiness-plan.md` — ordem, responsáveis e aceite;
- `research-production-readiness.md` — decisões e alternativas;
- `contracts/production-readiness-gates.md` — contrato de Go/No-Go;
- `production-readiness-quickstart.md` — validação da candidata.

Esse incremento não cria entidades de domínio nem altera o escopo funcional. Ele fecha lacunas de **FR-001**, **FR-004**, **NFR-005**, **NFR-006**, **NFR-008** e dos gates da constituição.
