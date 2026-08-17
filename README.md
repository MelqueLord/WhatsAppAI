# WhatsApp AI Manager

Nome provisório de um SaaS multiempresa para centralizar atendimentos do WhatsApp Business e automatizar respostas com IA.

## Estado do projeto

O pacote SDD inicial foi implementado. O backlog em `specs/000-platform/tasks.md` esta marcado como concluido ate `T144`, cobrindo bootstrap, identidade/tenancy, WhatsApp, inbox, resposta humana, IA segura, conhecimento, uso/auditoria, producao/piloto e sistema de planos.

Implementado:

- Backend .NET 10 com WebApi, workers, EF Core, autenticacao, tenant isolation, SignalR, Meta/OpenAI, Inbox/Outbox, auditoria e uso.
- Frontend React 19.2 + TypeScript + Vite com telas de auth, admin, operadores, inbox, integracoes, conhecimento, uso, bot e planos.
- Persistencia MySQL 8.4 LTS em producao/testes e SQLite em desenvolvimento local.
- Docker, Nginx, scripts de backup/restore, observabilidade, runbooks e testes unitarios/integracao/arquitetura.

Falta validar antes de considerar pronto para piloto real:

- Rodar a suite completa local/CI e revisar resultados recentes.
- Confirmar limpeza de artefatos locais versionados, especialmente arquivos SQLite de desenvolvimento.
- Revisar uso de migrations versus `EnsureCreatedAsync()` antes de producao.
- Executar checklist de deploy/piloto com credenciais reais em cofre.

## Premissas fechadas

- Cada cliente é dono da conta Meta, do número, do método de pagamento e do projeto/chave da OpenAI.
- O produto usa somente a API oficial WhatsApp Cloud API.
- O MVP atende conversas iniciadas pelo consumidor; não inclui campanhas nem disparos de marketing.
- O núcleo não depende de n8n.
- A arquitetura inicial é um monólito modular, sem microsserviços, RabbitMQ ou Redis.
- Stack de referência: .NET 10 LTS, React 19.2 + TypeScript, MySQL 8.4 LTS, SQLite local e SignalR.

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

## Próximo marco

Validar a implementacao completa com build/testes, revisar riscos operacionais pendentes e executar o checklist de deploy/piloto.
