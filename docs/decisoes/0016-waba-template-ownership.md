# ADR-0016 — WABA como fronteira de propriedade dos templates

**Status:** Aceito

**Data:** 2026-09-26

**Relacionados:** US-017, FR-084 a FR-088, BR-053 a BR-055, ADR-0002, ADR-0005

## Contexto

O modelo atual representa cada linha oficial por `WhatsAppAccount` e repete `WabaId` nesse registro. Isso funciona para mensagens, cujos webhooks carregam `phone_number_id`, mas não fornece uma fronteira inequívoca para templates. Templates são ativos da WABA e seus eventos usam o identificador da WABA em `entry.id`.

Uma WABA pode reunir várias linhas do mesmo tenant. Sem uma entidade própria e unicidade global do identificador externo, o catálogo seria duplicado por linha e um evento de template poderia ser associado a mais de um tenant por configuração incorreta.

## Decisão

Criar `WhatsAppBusinessAccount` como agregado tenant-owned e tornar `WabaId` globalmente único na instalação. Linhas `OfficialApi` passam a referenciar esse agregado; tokens continuam associados às linhas até que outra necessidade demonstre vantagem em movê-los.

Templates pertencem a `WhatsAppBusinessAccount` e são únicos por WABA, nome e idioma. Uma linha selecionada pelo TenantOwner fornece a credencial usada para criar ou sincronizar o catálogo, mas não altera a propriedade do template.

Webhooks continuam validados pelo `app_secret` global antes da resolução. Eventos de mensagens usam `phone_number_id`; eventos de templates usam a WABA. O Inbox de webhooks passa a registrar explicitamente o tipo e o valor do escopo de roteamento.

A migration agrupa linhas existentes por tenant e WABA, cria as relações e instala a unicidade. Se a mesma WABA já estiver associada a tenants diferentes, a migration falha com diagnóstico operacional; nenhum vínculo é escolhido silenciosamente.

## Alternativas consideradas

### Manter `WabaId` somente na linha

Rejeitada porque duplica o catálogo quando várias linhas compartilham a WABA e não impede associação cruzada entre tenants.

### Resolver o tenant procurando qualquer linha com o `WabaId`

Rejeitada porque a busca pode ser ambígua e transforma um erro de configuração em risco de isolamento.

### Criar um catálogo independente por linha

Rejeitada porque contradiz a propriedade definida pela Meta e exige sincronizações redundantes do mesmo ativo.

## Consequências

- A migration altera `WhatsAppAccount` e precisa de `Up` e `Down` testados no PostgreSQL.
- Consultas autenticadas continuam derivando o tenant da sessão e selecionam linha/WABA no servidor.
- Workers e webhooks sem sessão usam métodos explícitos, sempre validando `TenantId` e `WhatsAppBusinessAccountId` juntos.
- A Inbox e o broadcast podem continuar revalidando templates diretamente na Meta; o catálogo local serve à gestão e não reduz as barreiras existentes de envio.
- Não são adicionados serviços externos, broker ou outra unidade implantável.
