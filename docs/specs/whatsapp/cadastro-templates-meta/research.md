# Research: Cadastro de templates na Meta

## Criação e escopo do primeiro incremento

**Decision**: criar templates com `POST /{WABA_ID}/message_templates`, usando `name`, `language`, `category`, `parameter_format = POSITIONAL` e componentes `BODY` obrigatório e `FOOTER` opcional. Categorias aceitas localmente: `UTILITY` e `MARKETING`.

**Rationale**: templates são ativos da WABA. Esse recorte coincide com os componentes já aceitos pelo envio atual e evita oferecer cadastro de algo que a plataforma não consegue usar.

**Alternatives considered**: aceitar o JSON completo da Meta foi rejeitado por ampliar superfície de validação e incompatibilidade; cadastrar apenas Utilidade foi rejeitado porque a Inbox já permite Marketing individual aprovado.

**Sources**: [Template fundamentals](https://developers.facebook.com/documentation/business-messaging/whatsapp/templates/overview), [Template components](https://developers.facebook.com/documentation/business-messaging/whatsapp/templates/components).

## Permissões

**Decision**: exigir `whatsapp_business_management` para cadastro, catálogo e webhooks de template, mantendo `whatsapp_business_messaging` para os envios. Documentar Advanced Access/App Review para WABAs pertencentes aos clientes.

**Rationale**: autorização de envio não implica autorização para administrar templates. A falha deve ser distinguida de credencial inválida ou indisponibilidade.

**Alternatives considered**: presumir que o token atual basta foi rejeitado porque a Meta separa as permissões.

**Source**: [WhatsApp permissions](https://developers.facebook.com/documentation/business-messaging/whatsapp/permissions).

## Propriedade da WABA

**Decision**: normalizar `WhatsAppBusinessAccount`, globalmente único por `WabaId` e tenant-owned; linhas oficiais referenciam essa entidade e templates pertencem a ela.

**Rationale**: uma WABA pode conter várias linhas, enquanto o webhook de template identifica a WABA. O modelo atual repete `WabaId` por linha e não impede o mesmo ID em tenants diferentes.

**Alternatives considered**: catálogo por linha duplica ativos; consulta livre por `WhatsAppAccount.WabaId` pode ser ambígua. A decisão completa está no ADR-0016.

## Submissão durável e idempotência

**Decision**: persistir `WhatsAppTemplateSubmission` e processá-la por worker PostgreSQL. O endpoint exige `Idempotency-Key`; constraint única e fingerprint do request definem repetição idêntica ou conflito.

**Rationale**: a Meta não documenta idempotency key para criação. Um timeout pode ocorrer depois da criação remota; o servidor não pode repetir cegamente.

**Alternatives considered**: POST síncrono no endpoint deixa uma janela de duplicidade; reutilizar `OutboxMessage` exigiria generalizar uma entidade atualmente vinculada a `MessageId` e ao envio para contato.

## Reconciliação de resultado incerto

**Decision**: antes de uma tentativa recuperada e sempre depois de timeout/5xx incerto, consultar o catálogo por nome e filtrar idioma. Se o item remoto existir e o conteúdo normalizado corresponder, adotá-lo; se divergir, registrar conflito para intervenção.

**Rationale**: nome + idioma é a chave natural externa disponível. Erro de duplicidade `100/2388024` também deve iniciar reconciliação.

**Alternatives considered**: retry automático para todo timeout foi rejeitado; considerar qualquer duplicidade como sucesso foi rejeitado porque o conteúdo remoto pode ser diferente.

**Source**: [Template management](https://developers.facebook.com/documentation/business-messaging/whatsapp/templates/template-management).

## Estados separados

**Decision**: manter `SubmissionStatus` (`Queued`, `Processing`, `RetryScheduled`, `OutcomeUnknown`, `Reconciling`, `Accepted`, `FailedPermanent`, `NeedsAttention`) separado de `ReviewStatus` (`Unknown`, `Pending`, `Approved`, `Rejected`, `Paused`, `Disabled`, `Archived`, `Deleted`), preservando também o status bruto.

**Rationale**: aceite técnico da criação e aprovação de conteúdo são processos diferentes. Estados da Meta não são monotônicos; um template pode ser aprovado, pausado e reinstalado.

**Alternatives considered**: um único enum perde informação operacional; progressão monotônica rejeitaria eventos válidos posteriores.

## Webhooks e sincronização

**Decision**: processar `message_template_status_update`, `template_category_update` e `message_template_components_update` por WABA, persistindo cada `entry/change` separadamente. Sincronização paginada permanece como recuperação e fonte autoritativa.

**Rationale**: webhooks reduzem atraso, mas não substituem reconciliação. O receptor atual usa apenas o primeiro `phone_number_id` e descartaria eventos de template.

**Alternatives considered**: polling exclusivo aumenta chamadas e atraso; webhook exclusivo não recupera eventos perdidos nem mudanças externas anteriores.

**Sources**: [Status update webhook](https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/reference/message_template_status_update), [Managing webhooks](https://developers.facebook.com/documentation/business-messaging/whatsapp/solution-providers/manage-webhooks).

## Limites e validação

**Decision**: validar nome `^[a-z0-9_]+$` até 512 caracteres, corpo até 1.024, rodapé até 60, placeholders posicionais contíguos e exemplo para cada variável. Manter o limite local de até dez parâmetros compatível com o envio atual. Tratar limite externo de 100 criações por WABA/hora e `LIMIT_EXCEEDED` sem codificar capacidade comercial máxima rígida.

**Rationale**: validação antecipada melhora o formulário, mas a Meta continua como autoridade. A capacidade total depende do portfólio e pode mudar.

**Alternatives considered**: lista fechada de idiomas foi rejeitada por envelhecer; aceitar lacunas em placeholders foi rejeitado porque produz payload ambíguo.

**Sources**: [Template fundamentals](https://developers.facebook.com/documentation/business-messaging/whatsapp/templates/overview), [Template components](https://developers.facebook.com/documentation/business-messaging/whatsapp/templates/components).

## Versão da Graph API

**Decision**: centralizar e fixar `v26.0` em opções do adaptador Meta, removendo versões hardcoded distintas nas operações afetadas.

**Rationale**: a documentação oficial atual usa `v26.0` nos exemplos mais recentes; o projeto ainda fixa `v21.0`. Uma configuração única facilita atualização deliberada e testes de contrato.

**Alternatives considered**: manter `v21.0` foi rejeitado por risco de expiração; usar `latest` foi rejeitado por falta de reprodutibilidade.

## Atualização da interface

**Decision**: página própria para TenantOwner, catálogo local e polling de cinco segundos apenas enquanto houver estados não terminais; sincronização manual disponível.

**Rationale**: mantém a configuração de credenciais separada da gestão de conteúdo e atende a meta de atualização sem criar novo canal em tempo real.

**Alternatives considered**: embutir todo o cadastro em `WhatsAppConfigPage` tornaria a tela extensa; novo evento SignalR foi adiado porque polling condicionado atende o primeiro incremento.
