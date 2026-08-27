# Arquitetura do sistema

## Contexto

```mermaid
flowchart TB
  Customer[Cliente final] --> Meta[WhatsApp Cloud API]
  Customer --> QR[WhatsApp Web via Baileys/QR]
  Meta --> App[WhatsApp AI Manager]
  QR --> App
  Operator[Operador] --> App
  App --> OpenAI[OpenAI Responses API]
  App --> PostgreSQL[(PostgreSQL)]
```

Meta e OpenAI são contas do tenant. O navegador nunca recebe suas credenciais.

## Containers

```mermaid
flowchart TB
  Web[React SPA] -->|HTTPS + cookie| API[ASP.NET Core API]
  Web <-->|SignalR| API
  Meta[Meta webhook] --> API
  QR[Baileys bridge/QR] --> API
  API --> DB[(PostgreSQL)]
  Worker[Workers internos] --> DB
  Worker --> MetaAPI[Meta Graph API]
  Worker --> Baileys[Baileys/WhatsApp Web]
  Worker --> AIAPI[OpenAI API]
```

API e workers podem começar no mesmo processo/artefato. PostgreSQL guarda estado, Inbox e Outbox conforme ADR-0008.

## Módulos e dependências

```mermaid
flowchart LR
  WebApi --> Application
  Infrastructure --> Application
  Application --> Domain
  WebApi --> Infrastructure
```

`Domain` não referencia SDKs ou infraestrutura. `Application` define portas e casos de uso. `Infrastructure` adapta EF Core, Meta, Baileys, OpenAI e cofre. `WebApi` compõe dependências e traduz HTTP/SignalR.

## Sequência de mensagem automática

```mermaid
sequenceDiagram
  participant M as Meta ou Baileys
  participant A as API
  participant D as PostgreSQL
  participant W as Worker
  participant O as OpenAI
  M->>A: webhook assinado
  A->>D: INSERT Inbox (idempotente)
  A-->>M: 200
  W->>D: normaliza mensagem
  W->>O: decisão estruturada
  O-->>W: reply ou handoff
  W->>D: revalida versão e grava Outbox
  W->>M: envia resposta pelo canal da linha
```

## Fronteiras de segurança

- Internet → WebApi: TLS, rate limit, tamanho máximo, autenticação apropriada por rota.
- WebApi → tenant: contexto derivado da sessão/conexão, nunca do body.
- Worker → provedores: credenciais recuperadas por referência e mantidas somente em memória pelo tempo necessário.
- SignalR: associação ao grupo ocorre no servidor após autenticação e resolução do tenant.

## Evolução condicionada

A arquitetura admite separar workers, cache ou broker, mas somente após telemetria e ADR. Nenhuma dessas possibilidades autoriza dependências antecipadas.
