# Plano: disparo em massa por template da API Oficial

**Branch**: `whatsapp` | **Data**: 2026-09-24 | **Spec**: [disparo-templates-api-oficial.md](disparo-templates-api-oficial.md)

## Resumo

Adicionar um segundo modo à lista de transmissão: template `UTILITY` aprovado na API Oficial. QR/texto livre permanece inalterado. Cada destinatário receberá uma `Message` de template e uma `OutboxMessage` durável e idempotente; a Outbox, e não o worker de broadcast, chamará a Meta.

Estão fora do escopo: marketing, promoções, templates `AUTHENTICATION`, mídia, botões/cabeçalhos, agendamento, importação de listas e qualquer alteração no bridge QR.

## Contexto técnico

**Linguagem/versão**: C#/.NET 10; React 19.2, TypeScript e Vite

**Dependências**: ASP.NET Core, EF Core/Npgsql, SignalR, interfaces próprias da Cloud API

**Persistência**: PostgreSQL; migration reversível

**Testes**: xUnit (domínio, endpoints, integração) e Vitest (interface)

**Plataforma**: monólito WebApi + worker; frontend web

**Tipo**: aplicação web modular

**Meta**: até 500 contatos manualmente ou fotografia da fila; produção em lotes, sem bloqueio HTTP

**Limites**: linha oficial ativa do tenant; template `APPROVED` + `UTILITY`; só `BODY` texto, até 10 parâmetros de 1024 caracteres

**Escopo**: uma lista ativa por tenant; isolamento por tenant, linha e fila

## Verificação da constituição

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Situação | Controle |
|---|---|---|
| Atendimento, não campanhas | Aprovado | Somente aviso transacional `UTILITY`; marketing permanece fora. |
| Tenant e privacidade | Aprovado | `TenantId` em dados/consultas; sem parâmetros em logs ou SignalR. |
| Idempotência e observabilidade | Aprovado | Vínculo Message/Outbox por destinatário, chave de idempotência e progresso. |
| Arquitetura proporcional | Aprovado | Reuso de PostgreSQL, Outbox e cliente Meta; sem serviço novo. |
| Especificação executável | Aprovado | Especificação, pesquisa, dados, contrato e quickstart vinculados. |

Revisão pós-design aprovada; não exige ADR novo.

## Estrutura do projeto

### Documentação desta feature

```text
docs/specs/whatsapp/
├── disparo-templates-api-oficial.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/disparo-templates-api-oficial.openapi.yaml
```

### Código afetado
```text
src/
├── WhatsAppAI.Domain/Broadcast/
├── WhatsAppAI.Infrastructure/Persistence/ and Workers/
└── WhatsAppAI.WebApi/Broadcast/
apps/web/src/features/broadcast/ and lib/api.ts
```

**Decisão**: evoluir o monólito existente; o domínio não referencia SDK Meta e a entrega externa permanece na Outbox.

## Fases de implementação

### 1. Domínio, dados e autorização

Adicionar `DeliveryMode`, metadados do template e referência de mensagem/tentativa por destinatário. Criar migration com `Down`, índices e unicidade de materialização. Centralizar política de acesso: TenantOwner total; Operator somente na interseção de linha e fila atribuídas. Todas as consultas com `IgnoreQueryFilters` recebem filtro explícito por `TenantId`.

### 2. API e processamento durável

Estender criação/edição para os dois modos exclusivos e oferecer consulta de templates por linha. Revalidar no dispatch linha, token, `APPROVED`, `UTILITY`, nome, idioma e quantidade de parâmetros. O worker materializa conversa automática, mensagem e Outbox na mesma transação; o reconciliador atualiza `Queued`, `Sent` e `Failed` a partir da mensagem/Outbox. Cancelamento para produção nova, não para itens já assumidos.

### 3. Interface e validação

Exibir seleção explícita QR/texto versus Oficial/template transacional; restringir linhas/fila para Operator; carregar templates após escolher linha; deixar nome/idioma imutáveis. Cobrir regressão QR, autorização, tenant, revalidação, concorrência, 429/retry, cancelamento e build/lint.

## Riscos e controles

| Risco | Controle |
|---|---|
| Template muda após formulário | Revalidação no dispatch. |
| Reinício/concorrência duplica | Chave por lista/destinatário/tentativa e índice único. |
| Progresso falso ao enfileirar | Reconciliar do estado final da Message/Outbox. |
| Vazar parâmetros/telefone | Logs e SignalR só usam IDs e erros sanitizados. |
