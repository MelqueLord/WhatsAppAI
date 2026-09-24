# Tarefas: disparo em massa por template da API Oficial

## Dependências

`US1` (modelo e criação) → `US2` (dispatch durável) → `US3` (interface e operação).

## Fase 1 — Fundamentação

- [X] T001 Atualizar especificação QR e rastreabilidade em `docs/specs/whatsapp/broadcast-qrcode.md`
- [X] T002 Criar enumeração, invariantes e testes do domínio em `src/WhatsAppAI.Domain/Broadcast/` e `tests/WhatsAppAI.Domain.Tests/`
- [X] T003 Criar mapeamento EF e migration reversível em `src/WhatsAppAI.Infrastructure/Persistence/` e `src/WhatsAppAI.Infrastructure/Migrations/`

## Fase 2 — US1: criar lista oficial e respeitar escopo

**Objetivo:** Owner/Operator autorizado cria e consulta rascunho oficial apenas no escopo permitido.

- [X] T004 [US1] Aplicar política linha/fila e DTOs de criação/listagem em `src/WhatsAppAI.WebApi/Broadcast/BroadcastEndpoints.cs`
- [X] T005 [US1] Expor templates UTILITY por linha oficial em `src/WhatsAppAI.WebApi/Broadcast/BroadcastEndpoints.cs`
- [ ] T006 [US1] Cobrir autorização, isolamento e validação de template em `tests/WhatsAppAI.WebApi.Tests/`

## Fase 3 — US2: disparo durável

**Objetivo:** cada destinatário gera uma Message e Outbox idempotentes e o progresso reflete o estado final.

- [X] T007 [US2] Materializar template oficial via Message/Outbox em `src/WhatsAppAI.Infrastructure/Workers/BroadcastDispatchWorker.cs`
- [X] T008 [US2] Implementar reconciliação, retry e cancelamento em `src/WhatsAppAI.Infrastructure/Workers/BroadcastDispatchWorker.cs` e repositório
- [ ] T009 [US2] Cobrir idempotência, falhas e regressão QR em `tests/WhatsAppAI.Infrastructure.Tests/`

## Fase 4 — US3: interface e validação

**Objetivo:** a tela torna os dois canais claros e impede escolhas inválidas antes do envio.

- [X] T010 [US3] Atualizar contratos tipados em `apps/web/src/lib/api.ts`
- [X] T011 [US3] Implementar seletor de canal, linha e template em `apps/web/src/features/broadcast/BroadcastPage.tsx`
- [X] T012 [US3] Cobrir a interface em `apps/web/src/features/broadcast/BroadcastPage.test.tsx`

## Fase 5 — validação

- [ ] T013 Formatar, compilar, executar testes afetados e revisar diff
