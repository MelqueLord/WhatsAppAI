# Data Model: Cadastro de templates na Meta

## WhatsAppBusinessAccount

Representa a WABA como fronteira de propriedade do tenant.

| Campo | Regra |
|---|---|
| `Id` | UUID, chave primária |
| `TenantId` | obrigatório; filtro global e FK para tenant |
| `WabaId` | obrigatório, até 100 caracteres, único globalmente |
| `CreatedAt`, `UpdatedAt` | UTC |
| `Version` | concorrência otimista |

Relacionamentos: uma WABA possui uma ou mais linhas oficiais e muitos templates. Uma linha oficial pertence no máximo a uma WABA.

Migration: agrupar `WhatsAppAccount` existentes por `TenantId + WabaId`, criar a FK e instalar unicidade global. Se uma WABA estiver em tenants distintos, abortar com diagnóstico; não escolher proprietário automaticamente.

## WhatsAppMessageTemplate

Representa uma variante de idioma do template remoto e seu snapshot local.

| Campo | Regra |
|---|---|
| `Id` | UUID |
| `TenantId` | obrigatório |
| `WhatsAppBusinessAccountId` | obrigatório; mesma combinação de tenant |
| `MetaTemplateId` | opcional até aceite/sincronização; único filtrado por tenant/WABA |
| `Name` | `^[a-z0-9_]+$`, até 512 |
| `Language` | código de idioma/locale até 20; validado estruturalmente |
| `RequestedCategory` | `UTILITY` ou `MARKETING` para criação local |
| `EffectiveCategory` | valor vigente retornado pela Meta; preserva valores futuros como bruto |
| `ParameterFormat` | `POSITIONAL` no primeiro incremento |
| `BodyText` | obrigatório, até 1.024 |
| `FooterText` | opcional, até 60 |
| `BodyExamplesJson` | exemplos sanitizados e ordenados; não entram em logs |
| `BodyParameterCount` | 0–10; placeholders contíguos de 1 a N |
| `ComponentsJson` | snapshot normalizado/sanitizado da Meta |
| `ReviewStatus` | enum normalizado abaixo |
| `ProviderRawStatus` | valor bruto limitado para compatibilidade futura |
| `RejectionReason`, `Recommendation` | opcionais, sanitizados e truncados |
| `IsInboxCompatible` | derivado da política compartilhada |
| `IsBroadcastCompatible` | derivado da política compartilhada |
| `ProviderUpdatedAt`, `LastSyncedAt` | UTC, opcionais |
| `CreatedAt`, `UpdatedAt`, `Version` | auditoria e concorrência |

Unicidade: `(TenantId, WhatsAppBusinessAccountId, Name, Language)`. Índices e FKs tenant-owned começam por `TenantId`.

### ReviewStatus

`Unknown | Pending | Approved | Rejected | Paused | Disabled | Archived | Deleted`

O estado não é monotônico. Webhook com ordenação comprovada aplica o evento mais novo. Sem timestamp/ordem confiável e com possível regressão, o worker busca o snapshot remoto antes de alterar. `ProviderRawStatus` preserva eventos como `FLAGGED`, `LOCKED`, `IN_APPEAL`, `REINSTATED`, `PENDING_DELETION` e valores futuros.

Compatibilidade:

- Inbox: `Approved`, categoria efetiva `UTILITY|MARKETING`, somente `BODY|FOOTER`, até dez parâmetros.
- Broadcast: mesmos componentes, mas categoria efetiva apenas `UTILITY`.
- O envio continua revalidando a Meta; os flags locais orientam gestão e UI.

## WhatsAppTemplateSubmission

Comando durável e auditável para criação remota.

| Campo | Regra |
|---|---|
| `Id` | UUID |
| `TenantId` | obrigatório |
| `WhatsAppBusinessAccountId` | obrigatório e do mesmo tenant |
| `WhatsAppMessageTemplateId` | obrigatório |
| `SourceWhatsAppAccountId` | linha oficial cuja credencial será usada |
| `IdempotencyKey` | obrigatório; único com tenant |
| `RequestFingerprint` | hash do request normalizado |
| `Status` | máquina operacional abaixo |
| `AttemptCount` | inteiro não negativo |
| `NextAttemptAt`, `ClaimedAt`, `ClaimExpiresAt` | controle durável/lease |
| `LastErrorCode`, `LastErrorCategory`, `LastErrorMessage` | sanitizados |
| `CorrelationId` | diagnóstico |
| `AcceptedAt`, `CompletedAt`, `CreatedAt`, `UpdatedAt` | UTC |
| `Version` | concorrência otimista |

Unicidade: `(TenantId, IdempotencyKey)`. Mesma chave com fingerprint igual retorna a submissão existente; fingerprint diferente produz conflito.

### SubmissionStatus

```text
Queued -> Processing -> Accepted
                    -> RetryScheduled -> Processing
                    -> OutcomeUnknown -> Reconciling -> Accepted
                                                   -> RetryScheduled
                                                   -> NeedsAttention
                    -> FailedPermanent
```

- `RetryScheduled`: somente falha comprovadamente anterior ao aceite ou resposta transitória segura.
- `OutcomeUnknown`: timeout/interrupção depois de a requisição poder ter chegado à Meta.
- `Reconciling`: consulta por WABA/nome/idioma antes de qualquer repetição.
- `NeedsAttention`: item remoto encontrado com conteúdo incompatível ou reconciliação inconclusiva após limite.
- `Accepted` e `FailedPermanent` são terminais no primeiro incremento.

## WebhookEvent — evolução

Campos novos/alterados:

| Campo | Regra |
|---|---|
| `RoutingKind` | `PhoneNumber` ou `Waba` |
| `RoutingId` | identificador externo limitado; substitui dependência obrigatória de `PhoneNumberId` |
| `EventKind` | campo Meta, como `messages` ou `message_template_status_update` |
| `TenantId` | resolvido após assinatura; pode permanecer nulo em quarentena |

O receptor valida a assinatura do corpo original e decompõe todos os `entry/change`. Cada fragmento recebe chave determinística que inclui hash do corpo/fragmento, escopo e posição. Constraint única resolve corridas; duplicata retorna `200`.

Eventos sem WABA conhecida permanecem `Unknown` e não podem criar ou alterar dados tenant-owned.

## AuditLog

Novas ações sugeridas:

- `whatsapp_template.submission_queued`
- `whatsapp_template.submission_accepted`
- `whatsapp_template.submission_failed`
- `whatsapp_template.status_changed`
- `whatsapp_template.catalog_synchronized`

Metadados permitidos: IDs internos, nome do template, idioma, categoria/estado, resultado e correlação. Excluídos: token, body, exemplos, payload bruto, telefone e razão não sanitizada.

## Invariantes entre entidades

1. Tenant do template, submissão, WABA e linha de origem deve ser idêntico.
2. Linha de origem deve ser `OfficialApi`, ativa e referenciar a WABA da submissão.
3. Uma submissão aceita deve vincular `MetaTemplateId` ou ser seguida de reconciliação até obtê-lo.
4. Sincronização completa faz upsert por ID externo e fallback por WABA/nome/idioma.
5. Falha durante paginação nunca marca templates ausentes ou arquivados.
6. Alteração idempotente sem mudança material não gera nova auditoria.
