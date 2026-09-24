# Spec: Lista de Transmissão (Broadcast) — QR Code

**ID:** 001-broadcast-qrcode  
**Versão:** 1.0.0  
**Status:** Aprovada

## Problema

Operadores precisam enviar a mesma mensagem para múltiplos contatos de uma vez — avisos operacionais e follow-ups. Conexões QR Code permitem texto livre; a API Oficial possui um fluxo separado, limitado a templates transacionais `UTILITY` aprovados, em [disparo por template oficial](disparo-templates-api-oficial.md).

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
- Listas pela API Oficial neste fluxo QR; elas são tratadas exclusivamente pela especificação de templates transacionais
- Agendamento de envio (pode ser evolução futura)
- Anexos/mídia (somente texto por ora)
- Relatórios avançados de leitura/entrega (sem suporte no QR)

## Requisitos Funcionais

### FR-BR-001 — Criação
- O sistema permite criar uma lista com: nome (obrigatório), mensagem de texto (obrigatório, máx 4096 caracteres), e seleção de contatos ou de uma fila
- A seleção manual aceita de 1 a 500 contatos. Ao selecionar uma fila ativa do tenant, o operador pode incluir todos os contatos da fila, inclusive quando o total passa de 500, ou selecionar manualmente de 1 a 500 contatos pertencentes àquela fila
- Um contato pertence à fila para fins de disparo quando foi importado para ela ou tem uma conversa aberta atualmente atribuída a ela, mesmo que nunca tenha sido importado. Outras filas do tenant seguem a mesma regra quando selecionadas
- A busca de contatos da fila é limitada ao tenant e à fila selecionada; o backend valida novamente essa associação antes de criar a lista. Conversas encerradas ou removidas da fila não conferem associação operacional à fila
- Cada contato aparece uma única vez na fotografia da lista. A criação grava os destinatários em blocos de até 500 registros para suportar filas grandes sem deixar uma lista parcial

### FR-BR-002 — Seleção de linha
- Ao disparar, o operador seleciona qual linha QR Code ativa será usada para envio
- O sistema valida que a linha está conectada antes de iniciar

### FR-BR-003 — Disparo
- Cada contato recebe a mensagem individualmente (conversa separada). O worker busca somente destinatários pendentes e os envia em lotes de cinco, com intervalo entre mensagens
- Um destinatário marcado como enviado não volta aos lotes seguintes. O reenvio inclui somente destinatários que falharam
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

- **BR-BC-001:** Esta especificação usa somente linhas QR Code; o modo oficial é regido pela especificação de templates transacionais
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
