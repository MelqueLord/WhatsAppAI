# Spec: Disparo em massa por template da API Oficial

**ID:** 002-disparo-templates-api-oficial

**Versão:** 1.0.0

**Status:** Planejada

## Escopo

Permitir que uma lista de transmissão use uma linha ativa da Cloud API e um template Meta `UTILITY` aprovado, para avisos transacionais. QR Code/texto livre continua com o comportamento atual. Ficam excluídos `MARKETING`, `AUTHENTICATION`, promoções, campanhas, opt-in, mídia, botões, cabeçalhos e agendamento.

## Requisitos

- **FR-BOT-001:** lista usa exclusivamente `QrCodeText` ou `OfficialApiTemplate`.
- **FR-BOT-002:** lista oficial armazena linha, nome, idioma e parâmetros de `BODY` (0–10 textos de até 1024 caracteres); não aceita texto livre.
- **FR-BOT-003:** a opção de template vem da WABA da linha e inclui somente `APPROVED` + `UTILITY`; componentes dinâmicos fora do corpo são rejeitados nesta entrega.
- **FR-BOT-004:** destinatários seguem `FR-BR-001`: 1–500 manualmente ou fotografia da fila, todos no tenant.
- **FR-BOT-005:** TenantOwner opera todo o tenant. Operator só acessa a interseção de sua linha e fila atribuídas, em listagem, detalhe, criação, edição, dispatch, cancelamento e retry.
- **FR-BOT-006:** no dispatch o backend revalida linha, token, template, idioma e parâmetros na Meta; frontend não é fonte de verdade.
- **FR-BOT-007:** cada destinatário gera `Message` de template + `OutboxMessage` transacionais e idempotentes; somente a Outbox chama a Meta.
- **FR-BOT-008:** progresso distingue pendente, enfileirado, enviado e falho. Falha individual não para os demais; retry atende somente falhas finais.
- **FR-BOT-009:** cancelamento impede novos enfileiramentos, sem apagar itens já assumidos pela Outbox.
- **FR-BOT-010:** parâmetros/tokens não entram em logs, respostas de progresso ou SignalR; falhas são sanitizadas.
- **FR-BOT-011:** interface explica claramente QR/texto livre e Oficial/template transacional; nome/idioma selecionados não são editáveis livremente.

## Critérios de sucesso

- **SC-BOT-001:** 50 destinatários são materializados sem duplicidade mesmo após reinício de worker.
- **SC-BOT-002:** template `MARKETING`, inválido, reprovado ou com contagem errada não cria Outbox.
- **SC-BOT-003:** Operator não consulta nem opera lista fora do escopo, inclusive por URL direta.
- **SC-BOT-004:** falha e 429 de um destinatário não interrompem os demais e são recuperáveis.
- **SC-BOT-005:** fluxo QR existente segue enviando texto livre sem regressão.

## Compatibilidade

Esta especificação complementa `broadcast-qrcode.md`: substitui somente sua exclusão de API Oficial por este caminho separado e restrito. A especificação de plataforma, especialmente `FR-012`, `FR-059`, `BR-006` e `BR-029`, continua prevalecendo. Marketing requer especificação própria de consentimento.
