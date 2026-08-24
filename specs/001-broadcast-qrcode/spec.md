# Spec: Lista de Transmissão (Broadcast) — QR Code

**ID:** 001-broadcast-qrcode  
**Versão:** 1.0.0  
**Status:** Aprovada

## Problema

Operadores precisam enviar a mesma mensagem para múltiplos contatos de uma vez — promoções pontuais, avisos operacionais, follow-ups. Pela API Oficial do WhatsApp essa operação exige Templates (HSM), que não estão no MVP. Conexões QR Code não têm essa restrição: permitem envio de texto livre a qualquer momento.

## Atores

- **TenantOwner / Operator** — cria e dispara listas de transmissão
- **EndCustomer** — recebe a mensagem individualmente (não vê os outros destinatários)

## Escopo

### Incluído
- Criar lista de transmissão com nome, texto e lista de contatos
- Selecionar contatos individualmente ou por tag
- Disparar lista em uma linha QR Code ativa do tenant
- Acompanhar progresso do envio (total, enviados, falhos)
- Cancelar disparo em andamento
- Histórico de listas enviadas

### Excluído
- Listas via API Oficial (requer Templates — fora do MVP)
- Agendamento de envio (pode ser evolução futura)
- Anexos/mídia (somente texto por ora)
- Relatórios avançados de leitura/entrega (sem suporte no QR)

## Requisitos Funcionais

### FR-BR-001 — Criação
- O sistema permite criar uma lista com: nome (obrigatório), mensagem de texto (obrigatório, máx 4096 caracteres), e seleção de contatos
- Contatos podem ser selecionados individualmente ou via filtro por tag
- Mínimo de 1 contato; máximo de 500 por lista

### FR-BR-002 — Seleção de linha
- Ao disparar, o operador seleciona qual linha QR Code ativa será usada para envio
- O sistema valida que a linha está conectada antes de iniciar

### FR-BR-003 — Disparo
- Cada contato recebe a mensagem individualmente (conversa separada)
- Envios são enfileirados no Outbox existente (durable, com retry)
- Intervalo mínimo de 1–3 segundos entre envios (aleatório) para evitar bloqueio pelo WhatsApp
- Se a conversa não existir, ela é criada no modo `Automatic`

### FR-BR-004 — Progresso
- O operador vê em tempo real: total de destinatários, enviados, falhos
- SignalR notifica atualização de progresso

### FR-BR-005 — Cancelamento
- O operador pode cancelar um disparo em andamento
- Mensagens já enviadas não são revertidas; apenas as pendentes são descartadas

### FR-BR-006 — Histórico
- Listas disparadas ficam salvas com status (Rascunho, Disparando, Concluída, Cancelada) e métricas finais

## Regras de Negócio

- **BR-BC-001:** Somente linhas QR Code podem ser usadas
- **BR-BC-002:** O tenant deve ter pelo menos uma linha QR Code conectada
- **BR-BC-003:** Isolamento por tenant — um tenant nunca acessa contatos ou listas de outro
- **BR-BC-004:** Broadcast não altera o modo de conversas já existentes em modo `Human`
- **BR-BC-005:** Máximo de 1 broadcast em andamento por tenant por vez

## Entidades

### BroadcastList
| Campo | Tipo | Descrição |
|---|---|---|
| Id | UUID | PK |
| TenantId | UUID | FK — isolamento |
| Name | string | Nome da lista |
| Message | string | Texto a enviar |
| Status | enum | Draft, Sending, Completed, Cancelled |
| LinePhoneNumberId | string | Linha QR usada |
| TotalCount | int | Total de destinatários |
| SentCount | int | Enviados com sucesso |
| FailedCount | int | Falhas |
| CreatedAt | datetime | |
| StartedAt | datetime? | |
| FinishedAt | datetime? | |
| CreatedByUserId | UUID | Operador que criou |

### BroadcastRecipient
| Campo | Tipo | Descrição |
|---|---|---|
| Id | UUID | PK |
| BroadcastListId | UUID | FK |
| TenantId | UUID | FK — isolamento |
| ContactId | UUID | FK — Contact |
| Status | enum | Pending, Sent, Failed |
| ErrorMessage | string? | Motivo da falha |
| SentAt | datetime? | |

## Critérios de Sucesso

- SC-BR-001: Operador cria, dispara e acompanha uma lista de 50 contatos em menos de 2 minutos de configuração
- SC-BR-002: 100% dos destinatários marcados como Sent recebem a mensagem (verificado no WhatsApp do destinatário)
- SC-BR-003: Falhas individuais não interrompem o envio para os demais destinatários
- SC-BR-004: Nenhum contato de outro tenant aparece na seleção
- SC-BR-005: Cancelamento para novos envios em até 10 segundos após solicitação

## Premissas

- O bridge Node.js já suporta `POST /sessions/:sessionId/send-message` — nenhuma mudança no bridge necessária
- O Outbox existente é reutilizado para enfileirar os envios
- Intervalo entre envios é implementado no worker, não no domínio
