# Modelo de dados

## `BroadcastList`

| Campo | Regra |
|---|---|
| `DeliveryMode` | `QrCodeText` ou `OfficialApiTemplate`; backfill QR para registros atuais. |
| `TemplateName`, `TemplateLanguage` | Obrigatórios apenas no modo oficial. |
| `TemplateBodyParametersJson` | Até 10 valores; nunca aparece em lista/progresso/log. |
| `LinePhoneNumberId` | Obrigatória no rascunho oficial; QR preserva definição no dispatch. |

## `BroadcastRecipient`

| Campo | Regra |
|---|---|
| `OutboundMessageId` | Vínculo único com a mensagem criada; nulo antes da materialização. |
| `DispatchAttempt` | Começa em 1 e incrementa no retry de falha final. |
| `QueuedAt` | Distingue enfileirado de enviado. |

Transições: `Pending → Queued → Sent|Failed`; cancelamento pode produzir `Skipped` só antes de enfileirar. Índices: `(TenantId, Status)`, `(BroadcastListId, Status)`, unicidade por `(BroadcastListId, ContactId, DispatchAttempt)` e por `OutboundMessageId` não nulo. A migration terá `Up` e `Down` e testes de isolamento.

Parâmetros podem conter dados pessoais: ficam somente na lista/mensagem exigida pelo envio e nunca em log; qualquer criptografia nova de dado de negócio exige ADR.
