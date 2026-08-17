# WhatsApp AI Manager

Nome provisório de um SaaS multiempresa para centralizar atendimentos do WhatsApp Business e automatizar respostas com IA.

## Estado do projeto

O pacote SDD inicial esta definido e a implementacao esta no bootstrap da Fase 0.

Implementado:

- `T001`: solution .NET 10, `global.json` e projetos backend/testes.
- `T002`: SPA React 19.2 + TypeScript + Vite, com scripts de lint, teste e build.
- `T003`: PostgreSQL 18 via Docker Compose, EF Core/Npgsql e teste de conectividade sem schema de negocio.

Ainda falta implementar:

- `T004`: formatadores, analyzers, nullable/warnings como gate e testes reais de arquitetura.
- `T005`: CI reproduzivel com restore, lint, build, testes e `git diff --check`.
- `T006`: logging estruturado, correlation ID, Problem Details e health checks.
- Fase 1 em diante: identidade, tenancy, convites, inbox, Meta, resposta humana, IA segura, conhecimento, uso/auditoria e preparo do piloto.

O codigo de dominio ainda nao possui entidades de negocio; `AppDbContext` esta intencionalmente vazio ate a primeira migration real da Fase 1.

## Premissas fechadas

- Cada cliente é dono da conta Meta, do número, do método de pagamento e do projeto/chave da OpenAI.
- O produto usa somente a API oficial WhatsApp Cloud API.
- O MVP atende conversas iniciadas pelo consumidor; não inclui campanhas nem disparos de marketing.
- O núcleo não depende de n8n.
- A arquitetura inicial é um monólito modular, sem microsserviços, RabbitMQ ou Redis.
- Stack de referência: .NET 10 LTS, React 19.2 + TypeScript, PostgreSQL 18 e SignalR.

## Mapa da documentação

| Documento | Finalidade |
|---|---|
| `AGENTS.md` | Regras operacionais para agentes do Codex |
| `.specify/memory/constitution.md` | Princípios que governam todas as decisões |
| `specs/000-platform/spec.md` | Escopo, histórias, requisitos e critérios de sucesso |
| `specs/000-platform/plan.md` | Plano técnico e estrutura do código |
| `specs/000-platform/research.md` | Decisões e justificativas |
| `specs/000-platform/data-model.md` | Modelo de dados e invariantes |
| `specs/000-platform/contracts/openapi.yaml` | Contrato HTTP inicial |
| `specs/000-platform/tasks.md` | Backlog de implementação rastreável |
| `specs/000-platform/quickstart.md` | Sequência de preparação e execução local |
| `docs/architecture/architecture.md` | Visão de componentes e fluxos |
| `docs/architecture/adr/` | Registros de decisões arquiteturais |
| `docs/security/threat-model.md` | Ameaças, controles e privacidade |
| `docs/ai/behavior-policy.md` | Limites e comportamento da automação |
| `docs/testing/strategy.md` | Estratégia de testes e gates |
| `docs/runbooks/webhook-failures.md` | Operação de falhas de webhook |
| `docs/sdd-framework.md` | Framework SDD e skills recomendadas |

## Proximo marco

Concluir `T004`-`T006` para fechar a Fase 0 com qualidade automatizada, CI e observabilidade basica antes de iniciar identidade e tenancy na Fase 1.
