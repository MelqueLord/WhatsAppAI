# Implementation Plan: Cadastro de templates na Meta

**Branch**: `cadastro-templates-meta` | **Date**: 2026-09-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `docs/specs/whatsapp/cadastro-templates-meta/spec.md`

## Summary

Permitir ao TenantOwner criar templates textuais de Utilidade ou Marketing na WABA de uma linha oficial, acompanhar revisão e sincronizar alterações externas. A implementação normaliza a WABA como agregado tenant-owned, mantém um catálogo local por WABA/nome/idioma, usa submissão durável com reconciliação antes de repetir resultados incertos e amplia a Inbox de webhooks para rotear eventos tanto por telefone quanto por WABA.

O primeiro incremento aceita `BODY` textual obrigatório, `FOOTER` opcional e parâmetros posicionais com exemplos. Ele não altera as regras de envio existentes e não inclui edição, exclusão, apelação, Autenticação, cabeçalhos, mídia, botões ou parâmetros nomeados.

## Technical Context

**Language/Version**: C# / .NET 10 LTS; TypeScript 5.9; React 19.2

**Primary Dependencies**: ASP.NET Core, EF Core 10, Npgsql, `HttpClient`, React Router, TanStack Query e SignalR existente

**Storage**: PostgreSQL; segredos continuam em `ISecretStore`

**Testing**: xUnit unitário/integração/arquitetura, Testcontainers quando necessário, Vitest e Testing Library

**Target Platform**: aplicação web Linux/containers, com desenvolvimento Windows suportado

**Project Type**: monólito modular com WebApi, worker e SPA

**Performance Goals**: aceitar o cadastro local em até 5 segundos; refletir evento persistido na tela em até 10 segundos; processar webhooks sem aguardar chamadas de sincronização à Meta

**Constraints**: zero acesso cruzado; webhook reconhecido rapidamente; nenhum token, payload bruto ou conteúdo completo em logs; no máximo 100 criações por WABA/hora conforme limite externo; somente `APPROVED` e compatível pode ser enviado

**Scale/Scope**: capacidade inicial de 50 tenants; até 6.000 variantes por WABA conforme capacidade externa, com paginação; criação limitada a texto posicional e duas categorias

**External contract**: Graph API `v26.0`, centralizada em configuração única do adaptador Meta e atualizada somente de forma deliberada; permissões `whatsapp_business_management` e `whatsapp_business_messaging`

## Constitution Check

*GATE: aprovado antes da pesquisa e reavaliado após o design.*

| Princípio/gate | Avaliação |
|---|---|
| Simplicidade orientada ao atendimento | Passa. O incremento elimina dependência operacional do WhatsApp Manager e limita o primeiro corte aos componentes já enviáveis. |
| Integrações oficiais e responsabilidade clara | Passa. Usa a API oficial da Meta atrás de `IWhatsAppClient`; QR Code é rejeitado. |
| Controle humano | Passa. Somente TenantOwner cadastra e a Meta decide aprovação; backend continua decidindo o que é enviável. |
| Isolamento e privacidade | Passa. WABA torna-se globalmente inequívoca e todas as novas entidades carregam `TenantId`; segredos permanecem no cofre. |
| Entrega incremental e observável | Passa. Submissão durável, webhook idempotente, reconciliação, auditoria e erros sanitizados tratam falhas parciais. |
| Arquitetura proporcional | Passa. PostgreSQL e workers existentes bastam; nenhuma dependência, broker ou serviço novo é introduzido. |
| Especificação executável | Passa. US-017, FR-084–FR-088, BR-053–BR-055, ADR-0016 e os contratos deste diretório fornecem rastreabilidade. |
| Build/testes sem novos warnings | Planejado como gate final. |
| Isolamento de tenant | Testes com duas WABAs/tenants e migration com conflito explícito são obrigatórios. |
| Migration reversível | `Up`/`Down`, backfill e restauração do modelo anterior serão testados no PostgreSQL. |
| Logs e segredos | Auditoria usa IDs internos/correlação; body, exemplos, tokens e payload bruto não entram em logs. |

**Reavaliação pós-design**: aprovada. A decisão estrutural sobre propriedade da WABA está registrada no [ADR-0016](../../../decisoes/0016-waba-template-ownership.md); não restam violações a justificar.

## Project Structure

### Documentation (this feature)

```text
docs/specs/whatsapp/cadastro-templates-meta/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── checklists/
│   └── requirements.md
└── contracts/
    ├── template-management.openapi.yaml
    └── meta-template-events.md
```

### Source Code (repository root)

```text
src/
├── WhatsAppAI.Domain/Integrations/
│   ├── WhatsAppBusinessAccount.cs
│   ├── WhatsAppMessageTemplate.cs
│   └── WhatsAppTemplateSubmission.cs
├── WhatsAppAI.Application/
│   ├── Abstractions/
│   │   ├── IWhatsAppBusinessAccountRepository.cs
│   │   └── IWhatsAppMessageTemplateRepository.cs
│   └── Integrations/IWhatsAppClient.cs
├── WhatsAppAI.Infrastructure/
│   ├── Meta/WhatsAppClient.cs
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   ├── Configurations/
│   │   ├── Repositories/
│   │   └── Migrations/
│   └── Workers/
│       ├── WhatsAppTemplateSubmissionWorker.cs
│       └── WebhookProcessingWorker.cs
└── WhatsAppAI.WebApi/
    ├── Integrations/WhatsAppTemplateEndpoints.cs
    └── Webhooks/WebhookEndpoints.cs

apps/web/src/
├── features/integrations/whatsapp/templates/
│   ├── WhatsAppTemplatesPage.tsx
│   └── WhatsAppTemplatesPage.test.tsx
├── lib/api.ts
├── App.tsx
└── components/Sidebar.tsx

tests/
├── WhatsAppAI.UnitTests/
│   ├── Integrations/WhatsAppMessageTemplateTests.cs
│   ├── Meta/MetaClientAuthorizationTests.cs
│   └── Workers/WhatsAppTemplateSubmissionWorkerTests.cs
└── WhatsAppAI.IntegrationTests/
    ├── Integrations/WhatsAppTemplateEndpointsTests.cs
    ├── Persistence/WhatsAppTemplatePersistenceTests.cs
    └── Webhooks/WebhookTests.cs
```

**Structure Decision**: manter o monólito modular atual. Domain contém invariantes e estados; Application define portas; Infrastructure implementa Graph API, PostgreSQL e workers; WebApi autoriza e traduz contratos; React oferece a gestão. A entidade de submissão atua como fila durável especializada porque `OutboxMessage` atual exige `MessageId` e representa envio ao cliente.

## Design Phases

### 1. Normalizar a propriedade da WABA

- Implementar o ADR-0016 com `WhatsAppBusinessAccount` tenant-owned e `WabaId` globalmente único.
- Referenciar a WABA a partir de cada linha oficial, preservando token por linha.
- Fazer backfill agrupando linhas de um tenant pela WABA e falhar explicitamente se o mesmo identificador já estiver em tenants diferentes.
- Adicionar filtros globais e métodos explícitos para workers sem sessão.

### 2. Criar catálogo e submissão durável

- Persistir `WhatsAppMessageTemplate` por WABA, nome e idioma.
- Separar estado operacional da submissão do estado de revisão da Meta.
- Exigir `Idempotency-Key`; mesma chave e mesmo fingerprint retornam a operação, enquanto payload diferente retorna conflito.
- Fazer claim atômico de submissões, recuperar claims abandonados e usar backoff somente para falhas transitórias comprovadas.
- Após timeout ou resposta incerta, consultar a Meta por nome e idioma antes de qualquer novo `POST`.

### 3. Ampliar o adaptador Meta

- Centralizar a versão Graph configurada e remover o `v21.0` duplicado/hardcoded das operações afetadas.
- Adicionar criação de template e enriquecer listagem com ID externo, componentes, formato, categoria, status e dados sanitizados de rejeição.
- Mapear `400/100`, duplicidade `2388024`, `403/200`, `429`, `5xx` e timeout em resultados tipados, distinguindo falha permanente, transitória e resultado incerto.
- Manter autorização por requisição e nenhuma credencial em headers globais ou logs.

### 4. Expor gestão autenticada

- Criar endpoints do contrato [template-management.openapi.yaml](contracts/template-management.openapi.yaml).
- Resolver tenant da sessão e WABA pela `lineNumber`; o navegador nunca escolhe `TenantId` ou `WabaId` arbitrário.
- Validar nome, idioma, categoria, limites, sequência de placeholders e exemplos antes de enfileirar.
- Auditar criação e sincronização com metadados mínimos.

### 5. Processar eventos por WABA

- Evoluir `WebhookEvent` para `RoutingKind` e `RoutingId`, preservando compatibilidade com eventos por telefone.
- Depois da assinatura válida, decompor todos os `entry/change` do POST e persistir cada fragmento antes do processamento.
- Resolver `message_template_status_update`, `template_category_update` e `message_template_components_update` pela WABA.
- Deduplicar pela identidade disponível ou por hash determinístico do fragmento; colisão concorrente retorna sucesso ao provedor.
- Atualizar por ID externo, com fallback por nome/idioma; evento sem ordenação confiável provoca leitura autoritativa antes de regredir estado.

### 6. Sincronizar catálogo

- Listar todas as páginas da WABA e fazer upsert por ID externo ou nome/idioma.
- Somente após paginação integral bem-sucedida marcar como ausente/arquivado um item não retornado.
- Manter a revalidação remota atual na Inbox e no broadcast; o catálogo local não vira autorização de envio.
- Assinar os campos de webhook da WABA no App Meta compartilhado e documentar a verificação operacional.

### 7. Entregar a experiência do TenantOwner

- Criar página própria com seleção da linha oficial, explicando quando o catálogo é compartilhado por mais de uma linha da mesma WABA.
- Exibir formulário guiado, preview, contadores, exemplos fictícios, catálogo por categoria/status e compatibilidade.
- Fazer polling de cinco segundos apenas enquanto houver submissões ou revisões não terminais; sincronização manual permanece disponível.
- Esconder rota e navegação de Operator e bloquear novamente no backend.

## Verification Strategy

- Domínio: validações, fingerprints, idempotência e transições operacionais/revisão.
- Contrato Meta: payload de criação, paginação, autorização por request, códigos permanentes/transitórios, timeout e sanitização.
- Persistência: migration `Up`/`Down`, backfill de várias linhas na mesma WABA, conflito entre tenants, constraints e query filters.
- Worker: claim concorrente, recuperação, retry seletivo, reconciliação após timeout e tenant/linha inativos.
- API: cookie/CSRF, somente TenantOwner, QR rejeitado, linha fora da quota, token ausente, `202`, duplicidade e dois tenants.
- Webhook: assinatura, múltiplos entries/changes, WABA desconhecida, duplicidade concorrente, aprovação/rejeição/categoria/componentes e eventos fora de ordem.
- Frontend: formulário, placeholders, exemplos, status, rejeição, sincronização, ausência de linha, rota/Sidebar e layout móvel.
- Regressão: listagem/envio individual e broadcast existentes continuam revalidando a Meta e QR continua sem templates.
- Gates finais: `dotnet format --verify-no-changes`, build Release, testes unitários/arquitetura/integração relevantes, `npm run lint`, `npm test`, `npm run build` e `git diff --check`.

## Risks and Mitigations

| Risco | Mitigação planejada |
|---|---|
| WABA ligada a tenants diferentes | Unicidade global, backfill que falha e teste negativo antes do deploy. |
| POST criado na Meta com resposta perdida | Estado `OutcomeUnknown` e reconciliação por nome/idioma antes de repetir. |
| Token envia mensagens mas não gerencia templates | Teste de permissão e erro específico para `whatsapp_business_management`; Advanced Access documentado. |
| Webhook sem `phone_number_id` descartado | Roteamento explícito por WABA e persistência por fragmento. |
| Evento duplicado ou fora de ordem | Chave determinística, constraint única e leitura autoritativa quando a ordem não é comprovável. |
| Categoria alterada pela Meta | Separar categoria solicitada e efetiva; regras de envio usam a efetiva remota. |
| Templates externos não aparecem | Sincronização paginada manual e recuperável, além dos webhooks. |
| Versão Graph expira | Versão única configurada e pinada; atualização deliberada com testes de contrato. |
| Conteúdo/exemplos vazam em logs | Allowlist de auditoria, mensagens sanitizadas e testes com sentinelas. |

## Complexity Tracking

Nenhuma violação constitucional. A nova entidade de WABA substitui a duplicação implícita por linha e está justificada no ADR-0016; o worker especializado reutiliza PostgreSQL e evita generalizar prematuramente a Outbox de mensagens.
